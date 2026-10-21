import { mkdir, writeFile } from "node:fs/promises";
import { createHash } from "node:crypto";

const directory = new URL("../wwwroot/vendor/", import.meta.url);
await mkdir(directory, { recursive: true });
for (const [source, destination, sha256] of [
    ["dist/adaptivecards.min.js", "adaptivecards.min.js", "d2c69fcd5793269bd4939c18f278d74fd24457e245c0845fc13964bf72834a41"],
    ["LICENSE", "adaptivecards.LICENSE", "f4537a9a339834394f5242176a4ed19b239a88a600bcb02041205f48b3c07dfc"]
]) {
    const url = `https://cdn.jsdelivr.net/npm/adaptivecards@3.0.5/${source}`;
    const response = await fetch(url);
    if (!response.ok) throw new Error(`${url}: HTTP ${response.status}`);
    const content = Buffer.from(await response.arrayBuffer());
    if (createHash("sha256").update(content).digest("hex") !== sha256)
        throw new Error(`Integrity check failed for ${url}`);
    await writeFile(new URL(destination, directory), content);
    console.log(`Verified ${destination}`);
}
