const tg = window.Telegram?.WebApp;
const content = document.getElementById("content");
const userLine = document.getElementById("user-line");
const screenTitle = document.getElementById("screen-title");
const homeButton = document.getElementById("home-button");

const state = {
    screen: "menu",
    menu: null,
    bank: null,
    selectedBankId: null
};

const currencyMeta = {
    hug: { name: "обнимашки", icon: "🤗" },
    kiss: { name: "поцелуйчики", icon: "💋" },
    spank: { name: "порка", icon: "🖐️" }
};

function initTelegram() {
    if (!tg) {
        userLine.textContent = "Открой через Telegram";
        content.innerHTML = '<p class="error">Mini App работает только внутри Telegram.</p>';
        return false;
    }

    tg.ready();
    tg.expand();
    document.body.style.backgroundColor = tg.themeParams.bg_color || "";
    return true;
}

async function apiPost(url, body) {
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ initData: tg.initData, ...body })
    });

    if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `Ошибка ${response.status}`);
    }

    return response.json();
}

async function apiUpload(url, formData) {
    formData.append("initData", tg.initData);
    const response = await fetch(url, { method: "POST", body: formData });
    if (!response.ok) {
        const payload = await response.json().catch(() => ({ message: "Ошибка загрузки" }));
        throw new Error(payload.message || "Ошибка загрузки");
    }
    return response.json();
}

async function loadMenu() {
    state.menu = await apiPost("/api/menu", {});
    userLine.textContent = state.menu.displayName
        ? `@${state.menu.displayName}`
        : `ID ${state.menu.userId}`;
    state.screen = "menu";
    state.selectedBankId = null;
    state.bank = null;
    render();
}

async function loadBank(bankId) {
    state.bank = await apiPost(`/api/banks/${bankId}`, {});
    state.selectedBankId = bankId;
    state.screen = "bank";
    render();
}

async function refreshBank() {
    if (state.selectedBankId) {
        await loadBank(state.selectedBankId);
    }
}

function render() {
    homeButton.classList.toggle("active", state.screen === "menu");

    if (state.screen === "menu") {
        screenTitle.textContent = "Мои банки";
        renderMenu();
        return;
    }

    if (state.screen === "bank") {
        screenTitle.textContent = state.bank?.name || "Банк";
        renderBank();
        return;
    }

    if (state.screen === "shop") {
        screenTitle.textContent = "Магазин";
        renderShop();
    }
}

function renderMenu() {
    const banks = state.menu?.banks || [];
    if (banks.length === 0) {
        content.innerHTML = `
            <div class="panel">
                <h2>Банков пока нет</h2>
                <p class="hint">Создай банк в боте командой <code>/create Название</code> или вступи по коду <code>/join</code>.</p>
            </div>`;
        return;
    }

    content.innerHTML = `
        <div class="panel">
            <h2>Выбери банк</h2>
            <div class="bank-list">
                ${banks.map(bank => `
                    <button class="bank-item" data-bank-id="${bank.id}">
                        <span>
                            <strong>${escapeHtml(bank.name)}</strong>
                            <span class="muted">${bank.isOwner ? "Ты создатель" : "Участник"}</span>
                        </span>
                        <span>›</span>
                    </button>`).join("")}
            </div>
        </div>`;

    content.querySelectorAll("[data-bank-id]").forEach(button => {
        button.addEventListener("click", () => loadBank(button.dataset.bankId).catch(showError));
    });
}

function renderBank() {
    const bank = state.bank;
    if (!bank) {
        return;
    }

    const cardVisual = bank.cardImageUrl
        ? `<img src="${bank.cardImageUrl}" alt="Карта">`
        : `<div class="bank-card-placeholder">💳</div>`;

    content.innerHTML = `
        <div class="bank-card-visual">${cardVisual}</div>

        <div class="panel">
            <h2>Твоя карта</h2>
            <p class="muted">Участников: ${bank.memberCount}</p>
            <div class="upload-block">
                <button class="ghost-button" id="upload-my-card">Загрузить свою картинку</button>
                <input class="file-input" id="my-card-file" type="file" accept="image/png,image/jpeg,image/webp">
            </div>
        </div>

        ${bank.canManage ? `
        <div class="panel owner-only">
            <h2>Управление банком</h2>
            <p class="muted">Только создатель банка может менять шаблон карты для всех участников.</p>
            <div class="upload-block">
                <button class="button" id="upload-template">Загрузить шаблон карты</button>
                <input class="file-input" id="template-file" type="file" accept="image/png,image/jpeg,image/webp">
            </div>
        </div>` : ""}

        <div class="panel">
            <h2>Валюты</h2>
            <div class="currency-grid">
                ${bank.currencies.map(currency => {
                    const meta = currencyMeta[currency.code] || { name: currency.name, icon: "💰" };
                    const amount = bank.balances?.[currency.code] ?? 0;
                    return `
                        <div class="currency-chip">
                            <div class="currency-chip-left">
                                <div class="currency-icon">${meta.icon}</div>
                                <div>
                                    <strong>${escapeHtml(meta.name)}</strong>
                                    <div class="muted">${escapeHtml(currency.code)}</div>
                                </div>
                            </div>
                            <strong>${amount}</strong>
                        </div>`;
                }).join("")}
            </div>
        </div>

        <button class="shop-entry" id="open-shop">
            <div class="shop-entry-icon">🛍️</div>
            <div>
                <strong>Магазин банка</strong>
                <div class="muted">${bank.products.length} товаров</div>
            </div>
        </button>`;

    bindUpload("upload-my-card", "my-card-file", `/api/banks/${bank.id}/my-card-image`);
    if (bank.canManage) {
        bindUpload("upload-template", "template-file", `/api/banks/${bank.id}/card-template`);
    }

    document.getElementById("open-shop").addEventListener("click", () => {
        state.screen = "shop";
        render();
    });
}

function renderShop() {
    const bank = state.bank;
    if (!bank) {
        return;
    }

    content.innerHTML = `
        <div class="panel">
            <button class="ghost-button" id="back-to-bank">← Назад к банку</button>
        </div>

        <div class="panel">
            <h2>Товары</h2>
            ${bank.products.length === 0
                ? '<p class="empty">Магазин пуст.</p>'
                : bank.products.map(product => {
                    const meta = currencyMeta[product.currencyCode] || { name: product.currencyCode };
                    return `
                        <div class="product-row">
                            <strong>${escapeHtml(product.name)}</strong>
                            <div class="muted">${product.price} ${escapeHtml(meta.name)}</div>
                        </div>`;
                }).join("")}
        </div>

        ${bank.canManage ? `
        <div class="panel owner-only">
            <h2>Добавить товар</h2>
            <form class="form-grid" id="add-product-form">
                <input name="name" placeholder="Название товара" required>
                <select name="currencyCode">
                    <option value="hug">обнимашки</option>
                    <option value="kiss">поцелуйчики</option>
                    <option value="spank">порка</option>
                </select>
                <input name="price" type="number" min="1" step="1" placeholder="Цена" required>
                <button class="button" type="submit">Добавить</button>
            </form>
        </div>` : `
        <div class="panel">
            <p class="hint">Добавлять товары может только создатель банка.</p>
        </div>`}`;

    document.getElementById("back-to-bank").addEventListener("click", () => {
        state.screen = "bank";
        render();
    });

    const form = document.getElementById("add-product-form");
    if (form) {
        form.addEventListener("submit", async event => {
            event.preventDefault();
            const data = new FormData(form);
            try {
                await apiPost(`/api/banks/${bank.id}/products`, {
                    name: data.get("name"),
                    currencyCode: data.get("currencyCode"),
                    price: Number(data.get("price"))
                });
                await refreshBank();
                state.screen = "shop";
                render();
            } catch (error) {
                showError(error);
            }
        });
    }
}

function bindUpload(buttonId, inputId, url) {
    const button = document.getElementById(buttonId);
    const input = document.getElementById(inputId);
    button.addEventListener("click", () => input.click());
    input.addEventListener("change", async () => {
        if (!input.files?.[0]) {
            return;
        }

        const formData = new FormData();
        formData.append("image", input.files[0]);
        try {
            await apiUpload(url, formData);
            await refreshBank();
            render();
        } catch (error) {
            showError(error);
        } finally {
            input.value = "";
        }
    });
}

function showError(error) {
    content.innerHTML = `<p class="error">${escapeHtml(error.message || String(error))}</p>`;
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
}

homeButton.addEventListener("click", () => {
    loadMenu().catch(showError);
});

if (initTelegram()) {
    loadMenu().catch(showError);
}
