import test from "node:test";
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { mkdtemp, mkdir, rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const base = process.env.WORKBENCH_URL ?? "http://localhost:5210";
const browser = process.env.BROWSER_PATH ?? "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const results = new URL("../test-results/", import.meta.url);

test("Blazor renderer, proposals, conflicts, inputs, cancellation and AG-UI transport", { timeout: 90000 }, async () => {
    await mkdir(results, { recursive: true });
    const profile = await mkdtemp(fileURLToPath(new URL("browser-", results)));
    const processHandle = spawn(browser, ["--headless=new", "--disable-gpu", "--no-first-run",
        "--remote-debugging-port=0", `--user-data-dir=${profile}`, "about:blank"], { stdio: ["ignore", "ignore", "pipe"] });
    let socket;
    try {
        const endpoint = await new Promise((resolve, reject) => {
            let output = "";
            const timeout = setTimeout(() => reject(new Error(`Browser did not start: ${output}`)), 15000);
            processHandle.once("error", reject);
            processHandle.stderr.on("data", chunk => {
                output += chunk;
                const match = output.match(/DevTools listening on (ws:\/\/\S+)/);
                if (match) { clearTimeout(timeout); resolve(match[1]); }
            });
        });
        socket = new WebSocket(endpoint);
        await once(socket, "open");
        let sequence = 0;
        const pending = new Map();
        const errors = [];
        socket.addEventListener("message", message => {
            const event = JSON.parse(message.data);
            if (event.method === "Runtime.exceptionThrown") errors.push(event.params.exceptionDetails.text);
            const resolver = pending.get(event.id);
            if (resolver) {
                pending.delete(event.id);
                event.error ? resolver.reject(new Error(JSON.stringify(event.error))) : resolver.resolve(event.result);
            }
        });
        const send = (method, params = {}, sessionId) => new Promise((resolve, reject) => {
            const id = ++sequence;
            pending.set(id, { resolve, reject });
            socket.send(JSON.stringify({ id, method, params, sessionId }));
        });
        const { targetId } = await send("Target.createTarget", { url: base });
        const { sessionId } = await send("Target.attachToTarget", { targetId, flatten: true });
        await send("Runtime.enable", {}, sessionId);
        const evaluate = async expression => {
            const result = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true, userGesture: true }, sessionId);
            if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
            return result.result.value;
        };
        const until = async (expression, message) => {
            for (let count = 0; count < 150; count++) {
                if (await evaluate(expression)) return;
                await new Promise(resolve => setTimeout(resolve, 100));
            }
            throw new Error(`Timed out: ${message}; page=${await evaluate("document.body.innerText")}`);
        };
        const click = text => evaluate(`[...document.querySelectorAll('button')].find(e=>e.textContent.trim()===${JSON.stringify(text)}).click()`);
        const edit = (selector, value) => evaluate(`{
            const e = document.querySelector(${JSON.stringify(selector)});
            e.value=${JSON.stringify(value)}; e.dispatchEvent(new Event('input',{bubbles:true}));
        }`);
        const firstPreview = Date.now();
        await until("document.querySelector('[data-testid=committed-preview] input')", "interactive initial card");
        console.log(`First preview observed: ${Date.now() - firstPreview} ms`);
        assert.match(await evaluate("document.querySelector('[data-testid=committed-preview]').innerText"), /Hello Adaptive Cards/);
        const initial = await evaluate("document.querySelector('#card-json').value");
        console.log(`Initial card: ${Buffer.byteLength(initial, "utf8")} UTF-8 bytes`);
        await evaluate(`globalThis.committedMutations = 0;
            new MutationObserver(() => committedMutations++).observe(
                document.querySelector('[data-testid=committed-preview]'), { childList:true, subtree:true });`);
        await edit("[data-testid=committed-preview] input", "retained input");
        await edit("#data-json", '{"name":"Edited user state"}');
        await until("document.querySelector('[data-testid=revision]').textContent==='1'", "draft revision");
        await click("Request proposal");
        await until("document.querySelector('[data-testid=proposal-preview] input')", "streamed proposal");
        console.log(await evaluate("document.querySelector('[data-testid=status]').innerText"));
        assert.match(await evaluate("document.querySelector('[data-testid=proposal-preview]').innerText"), /Hello Edited user state/);
        assert.equal(await evaluate("document.querySelector('[data-testid=committed-preview] input').value"), "retained input");
        assert.equal(await evaluate("committedMutations"), 0, "draft and proposal must not rerender committed DOM");
        await evaluate("document.querySelector('[data-testid=proposal-preview] button').click()");
        assert.equal(await evaluate("document.querySelector('[data-testid=revision]').textContent"), "1", "proposal actions are inert");
        await click("Accept proposal");
        await until("document.querySelector('[data-testid=committed-preview]').innerText.includes('Hello Edited user state')", "proposal accept");
        assert.equal(await evaluate("document.querySelector('[data-testid=committed-preview] input').value"), "retained input");
        await evaluate("document.querySelector('[data-testid=committed-preview] button').click()");
        await until("document.querySelector('[data-testid=proposal-preview]')?.innerText.includes('retained input')", "associated inputs round-trip");
        assert.match(await evaluate("document.querySelector('[data-testid=proposal-preview]').innerText"), /demo.submit/);
        await click("Reject proposal");
        const committed = await evaluate("document.querySelector('[data-testid=committed-preview]').innerText");
        await edit("#card-json", "{");
        await click("Validate and preview");
        await until("document.querySelector('[role=alert]')", "invalid draft diagnostic");
        assert.equal(await evaluate("document.querySelector('[data-testid=committed-preview]').innerText"), committed);
        await edit("#card-json", initial);
        await click("Request proposal");
        await edit("#data-json", '{"name":"Concurrent edit"}');
        await until("document.querySelector('[role=alert]')?.innerText.includes('stale')", "stale proposal rejected");
        assert.equal(await evaluate("document.querySelector('[data-testid=proposal-preview]')===null"), true);
        await click("Request proposal");
        await until("[...document.querySelectorAll('button')].some(e=>e.textContent.trim()==='Cancel run'&&!e.disabled)", "run started before cancel");
        await click("Cancel run");
        await until("document.querySelector('[data-testid=status]').innerText.includes('cancelled')", "cancellation");
        assert.equal(await evaluate("document.querySelector('#data-json').value"), '{"name":"Concurrent edit"}');
        const invalid = await fetch(`${base}/agent`, {
            method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ threadId: "t", runId: "r", state: {} })
        });
        assert.equal(invalid.status, 400);
        const disposal = await evaluate(`(async () => {
            const module = await import('/js/cardPreview.js');
            const host = document.createElement('div');
            document.body.append(host);
            let calls = 0;
            const instance = module.create(host, { invokeMethodAsync: async () => calls++ }, false);
            instance.render(${JSON.stringify(initial)});
            const button = host.querySelector('button');
            let failed = false;
            try { instance.render('{'); } catch { failed = true; }
            const retained = host.querySelector('button') === button;
            instance.dispose();
            button.click();
            host.remove();
            return { failed, retained, empty: host.childElementCount === 0, calls };
        })()`);
        assert.deepEqual(disposal, { failed: true, retained: true, empty: true, calls: 0 });
        assert.deepEqual(errors, []);
        await send("Browser.close");
    } finally {
        socket?.close();
        if (processHandle.exitCode === null) {
            const exited = once(processHandle, "exit");
            const exitedNormally = await Promise.race([exited.then(() => true), new Promise(resolve => setTimeout(() => resolve(false), 3000))]);
            if (!exitedNormally) processHandle.kill();
        }
        await rm(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 300 });
    }
});
