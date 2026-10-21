export function create(host, receiver, readOnly) {
    if (!globalThis.AdaptiveCards) throw new Error("Renderer assets are missing. Run npm ci && npm run build.");
    let card;
    let disposed = false;
    let pending = false;
    const release = () => {
        if (!card) return;
        card.onExecuteAction = undefined;
        card.onInputValueChanged = undefined;
        card.onAnchorClicked = undefined;
        card = undefined;
    };
    return {
        render(json) {
            if (disposed) throw new Error("The card preview is disposed.");
            const previous = new Map(card?.getAllInputs().map(input =>
                [input.id, { type: input.getJsonTypeName(), value: input.value }]) ?? []);
            const focused = card?.getAllInputs().find(input => input.renderedElement?.contains(document.activeElement))?.id;
            const next = new AdaptiveCards.AdaptiveCard();
            next.hostConfig = new AdaptiveCards.HostConfig({ fontFamily: "Segoe UI, sans-serif" });
            next.onAnchorClicked = () => true;
            next.onExecuteAction = async action => {
                if (readOnly || pending || disposed) return;
                pending = true;
                try {
                    await receiver.invokeMethodAsync("HandleAction", crypto.randomUUID(),
                        action.getJsonTypeName(), action.verb ?? null, action.data ?? {});
                } catch (error) {
                    if (!disposed) {
                        const message = document.createElement("p");
                        message.setAttribute("role", "alert");
                        message.textContent = `Action could not reach the server: ${error.message}`;
                        host.append(message);
                    }
                } finally { pending = false; }
            };
            next.parse(JSON.parse(json));
            for (const input of next.getAllInputs()) {
                const old = previous.get(input.id);
                if (old?.type === input.getJsonTypeName()) input.defaultValue = old.value;
            }
            const element = next.render();
            release();
            card = next;
            host.replaceChildren(element);
            if (focused) next.getAllInputs().find(input => input.id === focused)?.renderedElement
                ?.querySelector("input,textarea,select")?.focus();
            return next.validateProperties().validationEvents.map(event => event.message);
        },
        dispose() {
            disposed = true;
            release();
            receiver = undefined;
            host.replaceChildren();
        }
    };
}

export function download(name, json) {
    const url = URL.createObjectURL(new Blob([json], { type: "application/json" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = name;
    link.click();
    URL.revokeObjectURL(url);
}
