const tg = window.Telegram?.WebApp;
const content = document.getElementById("content");
const navBanks = document.getElementById("nav-banks");
const navHome = document.getElementById("nav-home");
const navAction = document.getElementById("nav-action");
const fileMyCard = document.getElementById("file-my-card");
const fileTemplate = document.getElementById("file-template");

const state = {
    screen: "menu",
    menu: null,
    bank: null,
    selectedBankId: null
};

const currencyMeta = {
    hug: { name: "Обнимашки", icon: "🤗", bar: "teal" },
    kiss: { name: "Поцелуйчики", icon: "💋", bar: "coral" },
    spank: { name: "Порка", icon: "🖐️", bar: "coral" }
};

function isLocalDev() {
    return location.hostname === "localhost" || location.hostname === "127.0.0.1";
}

function getInitData() {
    if (tg?.initData) {
        return tg.initData;
    }

    return isLocalDev() ? "local-dev" : "";
}

function initTelegram() {
    if (tg) {
        tg.ready();
        tg.expand();
        document.body.style.backgroundColor = tg.themeParams.secondary_bg_color || "#f3f6f8";
        return true;
    }

    if (isLocalDev()) {
        document.body.style.backgroundColor = "#f3f6f8";
        return true;
    }

    content.innerHTML = '<p class="error">Открой Mini App через Telegram.</p>';
    return false;
}

async function apiPost(url, body = {}) {
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ initData: getInitData(), ...body })
    });

    if (!response.ok) {
        throw new Error((await response.text()) || `Ошибка ${response.status}`);
    }

    return response.json();
}

async function apiUpload(url, file) {
    const formData = new FormData();
    formData.append("initData", getInitData());
    formData.append("image", file);
    const response = await fetch(url, { method: "POST", body: formData });
    if (!response.ok) {
        const payload = await response.json().catch(() => ({ message: "Ошибка загрузки" }));
        throw new Error(payload.message || "Ошибка загрузки");
    }
}

async function loadMenu() {
    state.menu = await apiPost("/api/menu");
    state.screen = "menu";
    state.bank = null;
    state.selectedBankId = null;
    render();
}

async function loadBank(bankId) {
    state.bank = await apiPost(`/api/banks/${bankId}`);
    state.selectedBankId = bankId;
    state.screen = "bank";
    render();
}

async function refreshBank() {
    if (state.selectedBankId) {
        await loadBank(state.selectedBankId);
    }
}

async function bootstrap() {
    state.menu = await apiPost("/api/menu");
    if (state.menu.banks.length === 1) {
        await loadBank(state.menu.banks[0].id);
        return;
    }

    state.screen = "menu";
    render();
}

function updateNav() {
    navBanks.classList.toggle("active", state.screen === "menu");
    navHome.classList.toggle("active", state.screen === "bank");
    navAction.classList.toggle("active", state.screen === "shop");
}

function render() {
    updateNav();

    if (state.screen === "menu") {
        renderMenu();
        return;
    }

    if (state.screen === "bank") {
        renderBank();
        return;
    }

    if (state.screen === "shop") {
        renderShop();
        return;
    }

    if (state.screen === "settings") {
        renderSettings();
    }
}

function renderMenu() {
    const banks = state.menu?.banks || [];
    const name = state.menu?.displayName || "друг";

    if (banks.length === 0) {
        content.innerHTML = `
            <div class="panel">
                <h2 class="section-title">Привет, ${escapeHtml(name)}</h2>
                <p class="hint">У тебя пока нет банка. Создай его в Telegram-боте или вступи по приглашению — потом вернись сюда.</p>
            </div>`;
        return;
    }

    content.innerHTML = `
        <h2 class="section-title">Выбери банк</h2>
        <div class="bank-pick">
            ${banks.map(bank => `
                <button class="bank-pick-card" data-bank-id="${bank.id}">
                    <strong>${escapeHtml(bank.name)}</strong>
                    <span>${bank.isOwner ? "Создатель банка" : "Участник"}</span>
                </button>`).join("")}
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

    const roleLabel = bank.isOwner ? "Owner" : "Member";
    const cardInner = bank.cardImageUrl
        ? `<img src="${bank.cardImageUrl}?t=${Date.now()}" alt="Карта">`
        : `<div class="credit-card-pattern"></div><div class="credit-card-chip"></div>`;

    const maxBalance = Math.max(10, ...bank.currencies.map(c => bank.balances?.[c.code] ?? 0));
    const lastTx = bank.transactions[0];

    content.innerHTML = `
        <div class="card-carousel">
            <div class="credit-card ${bank.cardImageUrl ? "has-image" : ""}" id="card-tap">
                ${cardInner}
                <div class="credit-card-top">${escapeHtml(bank.name)}</div>
                <div class="credit-card-bottom">${roleLabel}</div>
            </div>
            <div class="card-actions">
                <button class="pill-button" id="change-card-photo">Сменить фото карты</button>
                ${bank.canManage ? '<button class="pill-button primary" id="open-settings">Управление</button>' : ""}
            </div>
        </div>

        <div class="panel">
            <h2 class="section-title">Текущий баланс</h2>
            ${bank.currencies.map(currency => {
                const meta = currencyMeta[currency.code] || { name: currency.name, icon: "💰", bar: "coral" };
                const amount = bank.balances?.[currency.code] ?? 0;
                const width = Math.min(100, (amount / maxBalance) * 100);
                return `
                    <div class="debt-row">
                        <div class="debt-head">
                            <div class="debt-label">
                                <div class="debt-icon ${currency.code}">${meta.icon}</div>
                                <span>${escapeHtml(meta.name)}</span>
                            </div>
                            <strong>${amount}</strong>
                        </div>
                        <div class="debt-bar">
                            <div class="debt-bar-fill ${meta.bar}" style="width:${width}%"></div>
                        </div>
                    </div>`;
            }).join("")}
        </div>

        ${lastTx ? `
        <div class="panel">
            <h2 class="section-title">Последняя операция</h2>
            <div class="highlight-card">
                <div class="highlight-text">${formatTransaction(lastTx)}</div>
            </div>
        </div>` : ""}

        <div class="panel">
            <h2 class="section-title">Все операции</h2>
            ${bank.transactions.length === 0
                ? '<p class="empty">Операций пока нет</p>'
                : bank.transactions.map(tx => `
                    <div class="tx-item">
                        <div class="tx-left">
                            <div class="debt-icon ${tx.currencyCode}">${(currencyMeta[tx.currencyCode] || {}).icon || "💰"}</div>
                            <div>
                                <div>${escapeHtml(tx.description)}</div>
                                <div class="hint">${formatDate(tx.occurredAt)}</div>
                            </div>
                        </div>
                        <div class="tx-amount ${tx.amount >= 0 ? "positive" : "negative"}">
                            ${tx.amount >= 0 ? "+" : ""}${tx.amount}
                        </div>
                    </div>`).join("")}
        </div>`;

    document.getElementById("change-card-photo").addEventListener("click", () => fileMyCard.click());
    document.getElementById("card-tap").addEventListener("click", () => fileMyCard.click());

    const settingsButton = document.getElementById("open-settings");
    if (settingsButton) {
        settingsButton.addEventListener("click", () => {
            state.screen = "settings";
            render();
        });
    }
}

function renderShop() {
    const bank = state.bank;
    if (!bank) {
        return;
    }

    content.innerHTML = `
        <h2 class="section-title">Магазин</h2>
        <div class="shop-grid">
            ${bank.products.length === 0
                ? '<div class="panel empty">В магазине пока пусто</div>'
                : bank.products.map(product => {
                    const meta = currencyMeta[product.currencyCode] || { name: product.currencyCode };
                    return `
                        <div class="product-card">
                            <strong>${escapeHtml(product.name)}</strong>
                            <div class="product-price">${product.price} ${escapeHtml(meta.name)}</div>
                        </div>`;
                }).join("")}
        </div>`;
}

function renderSettings() {
    const bank = state.bank;
    if (!bank?.canManage) {
        content.innerHTML = '<p class="hint">Настройки доступны только создателю банка.</p>';
        return;
    }

    content.innerHTML = `
        <h2 class="section-title">Управление банком</h2>

        <div class="panel">
            <h2 class="section-title">Шаблон карты</h2>
            <p class="hint">Эта картинка будет у всех новых карт в банке.</p>
            <div class="settings-block">
                <button class="pill-button primary" id="upload-template-btn">Загрузить шаблон</button>
            </div>
        </div>

        <div class="panel">
            <h2 class="section-title">Новый товар</h2>
            <form class="form-card" id="add-product-form">
                <input name="name" placeholder="Название" required>
                <select name="currencyCode">
                    <option value="hug">Обнимашки</option>
                    <option value="kiss">Поцелуйчики</option>
                    <option value="spank">Порка</option>
                </select>
                <input name="price" type="number" min="1" step="1" placeholder="Цена" required>
                <button class="submit-button" type="submit">Добавить в магазин</button>
            </form>
        </div>`;

    document.getElementById("upload-template-btn").addEventListener("click", () => fileTemplate.click());

    document.getElementById("add-product-form").addEventListener("submit", async event => {
        event.preventDefault();
        const data = new FormData(event.target);
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

function formatTransaction(tx) {
    const meta = currencyMeta[tx.currencyCode] || { name: tx.currencyCode };
    const sign = tx.amount >= 0 ? "+" : "";
    return `${sign}${tx.amount} ${meta.name} · ${tx.description}`;
}

function formatDate(value) {
    return new Date(value).toLocaleString("ru-RU", {
        day: "2-digit",
        month: "short",
        hour: "2-digit",
        minute: "2-digit"
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

navBanks.addEventListener("click", () => loadMenu().catch(showError));

navHome.addEventListener("click", () => {
    if (state.selectedBankId) {
        state.screen = "bank";
        render();
        return;
    }

    loadMenu().catch(showError);
});

navAction.addEventListener("click", () => {
    if (!state.bank) {
        loadMenu().catch(showError);
        return;
    }

    state.screen = "shop";
    render();
});

fileMyCard.addEventListener("change", async () => {
    if (!fileMyCard.files?.[0] || !state.bank) {
        return;
    }

    try {
        await apiUpload(`/api/banks/${state.bank.id}/my-card-image`, fileMyCard.files[0]);
        await refreshBank();
    } catch (error) {
        showError(error);
    } finally {
        fileMyCard.value = "";
    }
});

fileTemplate.addEventListener("change", async () => {
    if (!fileTemplate.files?.[0] || !state.bank) {
        return;
    }

    try {
        await apiUpload(`/api/banks/${state.bank.id}/card-template`, fileTemplate.files[0]);
        await refreshBank();
        state.screen = "settings";
        render();
    } catch (error) {
        showError(error);
    } finally {
        fileTemplate.value = "";
    }
});

if (initTelegram()) {
    bootstrap().catch(showError);
}
