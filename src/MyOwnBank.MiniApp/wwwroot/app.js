const tg = window.Telegram?.WebApp;
const content = document.getElementById("content");
const userLine = document.getElementById("user-line");

let dashboard = null;
let activeTab = "home";

const currencyNames = {
    hug: "обнимашки",
    kiss: "поцелуйчики",
    spank: "порка"
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

async function loadDashboard() {
    const response = await fetch("/api/dashboard", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ initData: tg.initData })
    });

    if (!response.ok) {
        throw new Error("Не удалось авторизоваться через Telegram.");
    }

    dashboard = await response.json();
    userLine.textContent = dashboard.displayName
        ? `@${dashboard.displayName}`
        : `ID ${dashboard.userId}`;
    render();
}

function render() {
    if (!dashboard) {
        return;
    }

    if (!dashboard.hasBank) {
        content.innerHTML = `
            <div class="card">
                <h2>Банка пока нет</h2>
                <p class="muted">Создай банк в боте: <code>/create Название</code> или вступи по коду <code>/join</code>.</p>
            </div>`;
        return;
    }

    if (activeTab === "home") {
        content.innerHTML = `
            <div class="card">
                <h2>${escapeHtml(dashboard.bankName)}</h2>
                <p class="muted">Участников: ${dashboard.memberCount}${dashboard.isOwner ? " · ты владелец" : ""}</p>
            </div>
            <div class="card">
                <h2>Твой баланс</h2>
                <div class="balance-grid">${renderBalances(dashboard.balances)}</div>
            </div>`;
        return;
    }

    if (activeTab === "shop") {
        content.innerHTML = `
            <div class="card">
                <h2>Магазин</h2>
                ${dashboard.products.length === 0
                    ? '<p class="empty">Пока пусто. Владелец может добавить товары в боте.</p>'
                    : dashboard.products.map(renderProduct).join("")}
            </div>`;
        return;
    }

    content.innerHTML = `
        <div class="card">
            <h2>Последние операции</h2>
            ${dashboard.transactions.length === 0
                ? '<p class="empty">Операций пока нет.</p>'
                : dashboard.transactions.map(renderTransaction).join("")}
        </div>`;
}

function renderBalances(balances) {
    if (!balances || Object.keys(balances).length === 0) {
        return '<p class="empty">Баланс пуст.</p>';
    }

    return Object.entries(balances)
        .map(([code, amount]) => `
            <div class="balance-item">
                <span>${currencyNames[code] || code}</span>
                <strong>${amount}</strong>
            </div>`)
        .join("");
}

function renderProduct(product) {
    return `
        <div class="product">
            <strong>${escapeHtml(product.name)}</strong><br>
            <span class="muted">${product.price} ${currencyNames[product.currencyCode] || product.currencyCode}</span>
        </div>`;
}

function renderTransaction(item) {
    const date = new Date(item.occurredAt).toLocaleString("ru-RU", {
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
    });
    const sign = item.amount >= 0 ? "+" : "";

    return `
        <div class="transaction">
            <div>${escapeHtml(item.description)}</div>
            <span class="muted">${date} · ${sign}${item.amount} ${currencyNames[item.currencyCode] || item.currencyCode}</span>
        </div>`;
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
}

document.querySelectorAll(".tab").forEach(button => {
    button.addEventListener("click", () => {
        document.querySelectorAll(".tab").forEach(item => item.classList.remove("active"));
        button.classList.add("active");
        activeTab = button.dataset.tab;
        render();
    });
});

if (initTelegram()) {
    loadDashboard().catch(error => {
        content.innerHTML = `<p class="error">${escapeHtml(error.message)}</p>`;
    });
}
