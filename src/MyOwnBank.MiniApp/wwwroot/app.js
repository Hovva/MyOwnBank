const tg = window.Telegram?.WebApp;
const content = document.getElementById("content");
const navBanks = document.getElementById("nav-banks");
const navHome = document.getElementById("nav-home");
const navShop = document.getElementById("nav-shop");
const navCart = document.getElementById("nav-cart");
const navCartBadge = document.getElementById("nav-cart-badge");
const navSeparators = [
    document.getElementById("nav-separator-1"),
    document.getElementById("nav-separator-2"),
    document.getElementById("nav-separator-3")
];
const fileMyCard = document.getElementById("file-my-card");
const fileTemplate = document.getElementById("file-template");

const PREF_KEYS = {
    theme: "mybank.theme",
    displayName: "mybank.displayName"
};

const CART_STORAGE_PREFIX = "mybank.cart.";

const THEMES = [
    {
        id: "coral-slate",
        label: "Коралл и графит",
        dots: ["#ef6f6c", "#2d3436"],
        accent: "#ef6f6c",
        accentDark: "#d85a57",
        accentSoft: "rgba(239, 111, 108, 0.1)",
        iconActive: "#2d3436",
        iconMuted: "#9aa5b1",
        teal: "#2f9e9b",
        tealDark: "#1f7e7b",
        mint: "#d9f2f1"
    },
    {
        id: "teal-midnight",
        label: "Бирюза и полночь",
        dots: ["#2f9e9b", "#1a3d47"],
        accent: "#2f9e9b",
        accentDark: "#1f7e7b",
        accentSoft: "rgba(47, 158, 155, 0.12)",
        iconActive: "#1a3d47",
        iconMuted: "#8fa3ad",
        teal: "#3bb5b2",
        tealDark: "#1f7e7b",
        mint: "#d4f0ef"
    },
    {
        id: "rose-graphite",
        label: "Роза и графит",
        dots: ["#e5638a", "#2b2d42"],
        accent: "#e5638a",
        accentDark: "#cc4f74",
        accentSoft: "rgba(229, 99, 138, 0.12)",
        iconActive: "#2b2d42",
        iconMuted: "#9a9cb0",
        teal: "#6b7fd7",
        tealDark: "#5568c4",
        mint: "#f8e4ec"
    },
    {
        id: "amber-cocoa",
        label: "Янтарь и какао",
        dots: ["#e8a838", "#4a3728"],
        accent: "#e8a838",
        accentDark: "#cf9228",
        accentSoft: "rgba(232, 168, 56, 0.14)",
        iconActive: "#4a3728",
        iconMuted: "#a8947e",
        teal: "#c9783a",
        tealDark: "#a85f2c",
        mint: "#fbf0d8"
    },
    {
        id: "indigo-steel",
        label: "Индиго и сталь",
        dots: ["#5b6abf", "#2c3e50"],
        accent: "#5b6abf",
        accentDark: "#4a58a8",
        accentSoft: "rgba(91, 106, 191, 0.12)",
        iconActive: "#2c3e50",
        iconMuted: "#8b9aab",
        teal: "#4a90a4",
        tealDark: "#3a7586",
        mint: "#e8ebf8"
    },
    {
        id: "forest-pine",
        label: "Лес и сосна",
        dots: ["#22a559", "#1e3d32"],
        accent: "#22a559",
        accentDark: "#1a8a48",
        accentSoft: "rgba(34, 165, 89, 0.12)",
        iconActive: "#1e3d32",
        iconMuted: "#7f9a8e",
        teal: "#2f9e9b",
        tealDark: "#1f7e7b",
        mint: "#dff5e8"
    }
];

const state = {
    screen: "menu",
    menu: null,
    bank: null,
    selectedBankId: null,
    cardFlipped: false,
    creditMember: null,
    fineMember: null,
    memberSearch: "",
    showAddCurrency: false,
    inviteCode: null,
    transactionsExpanded: false,
    transactionsPage: [],
    transactionsHasMore: false,
    transactionsLoading: false,
    unreadNotifications: 0,
    notifications: [],
    notificationsHasMore: false,
    notificationsLoading: false,
    appSettingsReturnScreen: "menu",
    createBankReturnScreen: "menu"
};

const currencyBarColors = ["teal", "coral", "mint"];

const CURRENCY_IMAGE_PREFIX = "/currency-icons/";

function isCurrencyImageIcon(value) {
    return typeof value === "string" && value.startsWith(CURRENCY_IMAGE_PREFIX);
}

function resolveCurrencyIcon(bank, currency) {
    return currency?.icon || getCurrencyMeta(bank, currency?.code).icon;
}

function renderCurrencyIconDisplay(icon, className = "") {
    if (isCurrencyImageIcon(icon)) {
        return `<img src="${escapeHtml(icon)}" class="currency-icon-image ${className}" alt="">`;
    }

    return `<span class="currency-icon-emoji ${className}">${escapeHtml(icon || "💰")}</span>`;
}

function formatCurrencyIconText(icon) {
    return isCurrencyImageIcon(icon) ? "🖼" : (icon || "💰");
}

function renderCurrencySelect(bank, { id, name = "currencyCode", selectedCode = null } = {}) {
    const currencies = bank.currencies || [];
    const selected = currencies.find(item => item.code === selectedCode) || currencies[0];
    const selectedIcon = selected ? resolveCurrencyIcon(bank, selected) : "💰";

    return `
        <div class="currency-select" id="${escapeHtml(id)}" data-field-name="${escapeHtml(name)}">
            <input type="hidden" name="${escapeHtml(name)}" value="${selected ? escapeHtml(selected.code) : ""}" ${currencies.length ? "required" : ""}>
            <button type="button" class="currency-select__trigger" aria-haspopup="listbox">
                <span class="currency-select__icon">${renderCurrencyIconDisplay(selectedIcon, "currency-select__graphic")}</span>
                <span class="currency-select__label">${selected ? escapeHtml(selected.name) : "Валюта"}</span>
                <span class="currency-select__caret" aria-hidden="true">▾</span>
            </button>
            <div class="currency-select__menu hidden" role="listbox">
                ${currencies.map(currency => {
                    const iconValue = resolveCurrencyIcon(bank, currency);
                    const isSelected = selected?.code === currency.code;
                    return `
                        <button
                            type="button"
                            class="currency-select__option ${isSelected ? "is-selected" : ""}"
                            data-value="${escapeHtml(currency.code)}"
                            role="option"
                            aria-selected="${isSelected}">
                            <span class="currency-select__icon">${renderCurrencyIconDisplay(iconValue, "currency-select__graphic")}</span>
                            <span class="currency-select__label">${escapeHtml(currency.name)}</span>
                        </button>`;
                }).join("")}
            </div>
        </div>`;
}

function bindCurrencySelects(root = content) {
    root.querySelectorAll(".currency-select").forEach(select => {
        const hidden = select.querySelector('input[type="hidden"]');
        const trigger = select.querySelector(".currency-select__trigger");
        const menu = select.querySelector(".currency-select__menu");
        const triggerIcon = trigger.querySelector(".currency-select__icon");
        const triggerLabel = trigger.querySelector(".currency-select__label");

        const closeMenu = () => menu.classList.add("hidden");

        trigger.addEventListener("click", event => {
            event.stopPropagation();
            const willOpen = menu.classList.contains("hidden");
            root.querySelectorAll(".currency-select__menu").forEach(item => item.classList.add("hidden"));
            if (willOpen) {
                menu.classList.remove("hidden");
            }
        });

        select.querySelectorAll(".currency-select__option").forEach(option => {
            option.addEventListener("click", event => {
                event.stopPropagation();
                const value = option.dataset.value;
                hidden.value = value;
                triggerIcon.innerHTML = option.querySelector(".currency-select__icon").innerHTML;
                triggerLabel.textContent = option.querySelector(".currency-select__label").textContent;
                select.querySelectorAll(".currency-select__option").forEach(item => {
                    const active = item.dataset.value === value;
                    item.classList.toggle("is-selected", active);
                    item.setAttribute("aria-selected", active ? "true" : "false");
                });
                closeMenu();
            });
        });
    });

    if (!content.dataset.currencySelectBound) {
        content.dataset.currencySelectBound = "1";
        content.addEventListener("click", () => {
            content.querySelectorAll(".currency-select__menu").forEach(menu => menu.classList.add("hidden"));
        });
    }
}

function renderCurrencyIconPicker({ id, value = "⭐", currencyCode = null }) {
    const imageIcon = isCurrencyImageIcon(value);

    return `
        <div
            class="currency-icon-picker"
            data-picker-id="${escapeHtml(id)}"
            data-initial-icon="${escapeHtml(value)}"
            ${currencyCode ? `data-currency-code="${escapeHtml(currencyCode)}"` : ""}>
            <div class="currency-icon-picker__cell">
                <span class="currency-icon-picker__emoji ${imageIcon ? "hidden" : ""}">${imageIcon ? "⭐" : escapeHtml(value)}</span>
                <img class="currency-icon-picker__preview ${imageIcon ? "" : "hidden"}" ${imageIcon ? `src="${escapeHtml(value)}"` : ""} alt="">
                <label class="currency-icon-picker__upload" title="Загрузить картинку">
                    <input type="file" class="hidden-input currency-icon-picker__file" accept="image/png,image/jpeg,image/webp">
                    <span class="currency-icon-picker__upload-icon">+</span>
                </label>
            </div>
            <input
                type="text"
                class="currency-icon-picker__emoji-input ${imageIcon ? "hidden" : ""}"
                maxlength="8"
                value="${imageIcon ? "⭐" : escapeHtml(value)}"
                aria-label="Эмодзи">
        </div>`;
}

function bindCurrencyIconPickers(root = content) {
    root.querySelectorAll(".currency-icon-picker").forEach(picker => {
        const fileInput = picker.querySelector(".currency-icon-picker__file");
        const emojiSpan = picker.querySelector(".currency-icon-picker__emoji");
        const emojiInput = picker.querySelector(".currency-icon-picker__emoji-input");
        const preview = picker.querySelector(".currency-icon-picker__preview");
        const initialIcon = picker.dataset.initialIcon || "⭐";

        const showEmoji = emoji => {
            delete picker.dataset.imageUrl;
            delete picker.dataset.pendingFile;
            if (fileInput) {
                fileInput.value = "";
            }

            emojiSpan.textContent = emoji || "⭐";
            emojiSpan.classList.remove("hidden");
            preview.classList.add("hidden");
            preview.removeAttribute("src");
            if (emojiInput) {
                emojiInput.classList.remove("hidden");
                emojiInput.value = emoji || "⭐";
            }
        };

        const showImagePreview = src => {
            picker.dataset.imageUrl = src.startsWith("blob:") ? "" : src;
            picker.dataset.pendingFile = src.startsWith("blob:") ? "1" : "";
            preview.src = src;
            preview.classList.remove("hidden");
            emojiSpan.classList.add("hidden");
            if (emojiInput) {
                emojiInput.classList.add("hidden");
            }
        };

        if (isCurrencyImageIcon(initialIcon)) {
            picker.dataset.imageUrl = initialIcon;
            showImagePreview(initialIcon);
        }

        emojiInput?.addEventListener("input", () => {
            showEmoji(emojiInput.value.trim() || "⭐");
        });

        fileInput?.addEventListener("change", () => {
            const file = fileInput.files?.[0];
            if (!file) {
                return;
            }

            showImagePreview(URL.createObjectURL(file));
        });
    });
}

function readCurrencyIconPicker(picker) {
    const file = picker.querySelector(".currency-icon-picker__file")?.files?.[0];
    if (file) {
        return { type: "file", file };
    }

    if (isCurrencyImageIcon(picker.dataset.imageUrl)) {
        return { type: "url", url: picker.dataset.imageUrl };
    }

    const emoji = picker.querySelector(".currency-icon-picker__emoji-input")?.value.trim()
        || picker.querySelector(".currency-icon-picker__emoji")?.textContent.trim()
        || "⭐";

    return { type: "emoji", value: emoji };
}

async function uploadCurrencyIcon(bankId, currencyCode, file) {
    const formData = new FormData();
    formData.append("initData", getInitData());
    formData.append("image", file);

    const response = await fetch(
        `/api/banks/${bankId}/currencies/${encodeURIComponent(currencyCode)}/icon`,
        { method: "POST", body: formData });

    const text = await response.text();
    let payload = null;

    if (text) {
        try {
            payload = JSON.parse(text);
        } catch {
            payload = null;
        }
    }

    if (!response.ok) {
        throw new Error(payload?.message || payload?.Message || text || "Ошибка загрузки иконки");
    }

    return payload?.icon || payload?.Icon || null;
}

function getCurrencyAccent(bank, code) {
    const index = bank?.currencies?.findIndex(item => item.code === code) ?? 0;
    return currencyBarColors[Math.max(0, index) % currencyBarColors.length];
}

function getCurrencyMeta(bank, code) {
    const fromBank = bank?.currencies?.find(item => item.code === code);

    return {
        name: fromBank?.name || "Валюта",
        icon: fromBank?.icon || "💰",
        bar: getCurrencyAccent(bank, code)
    };
}

function icon(name, options = {}) {
    return AppIcons.render(name, options);
}

function mountNavIcons() {
    AppIcons.mount("#nav-banks-icon", "profile", { size: 26 });
    AppIcons.mount("#nav-shop-icon", "shop", { size: 26 });
    AppIcons.mount("#nav-cart-icon", "cart", { size: 26 });
}

function getCartStorageKey(bankId) {
    return `${CART_STORAGE_PREFIX}${bankId}`;
}

function loadCartItems(bankId) {
    if (!bankId) {
        return [];
    }

    try {
        const raw = localStorage.getItem(getCartStorageKey(bankId));
        return raw ? JSON.parse(raw) : [];
    } catch {
        return [];
    }
}

function saveCartItems(bankId, items) {
    if (!bankId) {
        return;
    }

    localStorage.setItem(getCartStorageKey(bankId), JSON.stringify(items));
}

function getCartItemCount(bankId) {
    return loadCartItems(bankId).reduce((sum, item) => sum + item.quantity, 0);
}

function syncCartBadge() {
    if (!navCartBadge) {
        return;
    }

    const count = getCartItemCount(state.selectedBankId);
    navCartBadge.textContent = String(count);
    navCartBadge.classList.toggle("hidden", count === 0);
}

function addProductToCart(bank, product) {
    const items = loadCartItems(bank.id);
    const existing = items.find(item => item.productId === product.id);
    if (existing) {
        existing.quantity += 1;
        existing.description = product.description || null;
    } else {
        items.push({
            productId: product.id,
            name: product.name,
            description: product.description || null,
            price: product.price,
            currencyCode: product.currencyCode,
            quantity: 1
        });
    }

    saveCartItems(bank.id, items);
    syncCartBadge();
}

function updateCartItemQuantity(bankId, productId, delta) {
    const items = loadCartItems(bankId)
        .map(item => item.productId === productId
            ? { ...item, quantity: item.quantity + delta }
            : item)
        .filter(item => item.quantity > 0);

    saveCartItems(bankId, items);
    syncCartBadge();
    return items;
}

function removeCartItem(bankId, productId) {
    const items = loadCartItems(bankId).filter(item => item.productId !== productId);
    saveCartItems(bankId, items);
    syncCartBadge();
    return items;
}

function clearCart(bankId) {
    saveCartItems(bankId, []);
    syncCartBadge();
}

function pruneCart(bank) {
    if (!bank?.id) {
        return;
    }

    const activeIds = new Set((bank.products || []).map(product => product.id));
    const items = loadCartItems(bank.id).filter(item => activeIds.has(item.productId));
    saveCartItems(bank.id, items);
}

function expandCartProductIds(items) {
    return items.flatMap(item => Array.from({ length: item.quantity }, () => item.productId));
}

async function ensureSelectedBankLoaded() {
    if (!state.selectedBankId) {
        return false;
    }

    if (state.bank?.id === state.selectedBankId) {
        return true;
    }

    try {
        state.bank = await apiPost(`/api/banks/${state.selectedBankId}`);
        return true;
    } catch (error) {
        if (isNotFoundError(error)) {
            await redirectToProfile(true);
            return false;
        }

        throw error;
    }
}

function syncNotificationBadge() {
    const badge = document.getElementById("notification-badge");
    if (!badge) {
        return;
    }

    const count = state.unreadNotifications || 0;
    badge.textContent = count > 99 ? "99+" : String(count);
    badge.classList.toggle("is-hidden", count <= 0);
}

function renderHeaderActions({ showNotifications = false } = {}) {
    return `
        <div class="bank-header__actions">
            ${showNotifications
                ? `<button class="icon-button notifications-trigger" type="button" aria-label="Уведомления">
                    ${icon("notification", { size: 20 })}
                    <span class="notification-badge ${state.unreadNotifications > 0 ? "" : "is-hidden"}" id="notification-badge">
                        ${state.unreadNotifications > 99 ? "99+" : state.unreadNotifications}
                    </span>
                </button>`
                : ""}
            <button class="icon-button icon-button--stroke app-settings-trigger" type="button" aria-label="Настройки приложения">
                ${icon("settings", { size: 20 })}
            </button>
        </div>`;
}

function renderPageHeader({ greeting, title, showNotifications = false }) {
    return `
        <div class="page-header">
            <div class="page-header__text">
                ${greeting ? `<p class="page-greeting">${greeting}</p>` : ""}
                ${title ? `<h2 class="section-title">${title}</h2>` : ""}
            </div>
            ${renderHeaderActions({ showNotifications })}
        </div>`;
}

function getThemeIndex(themeId) {
    const index = THEMES.findIndex(item => item.id === themeId);
    return index >= 0 ? index : 0;
}

function renderThemeSwitcher(themeId) {
    const theme = THEMES[getThemeIndex(themeId)];

    return `
        <div class="theme-switcher" id="theme-switcher">
            <label class="theme-switcher__option">
                <input type="radio" name="app-theme" value="${theme.id}" checked>
                <span class="theme-switcher__box" aria-hidden="true">
                    <span class="theme-switcher__dots" id="theme-switcher-dots">
                        ${theme.dots.map(color => `<span class="theme-dot" style="background:${color}"></span>`).join("")}
                    </span>
                </span>
            </label>
            <div class="theme-switcher__meta">
                <span class="theme-switcher__label" id="theme-switcher-label">${escapeHtml(theme.label)}</span>
                <div class="theme-switcher__arrows">
                    <button type="button" class="theme-arrow" id="theme-prev" aria-label="Предыдущий стиль">▲</button>
                    <button type="button" class="theme-arrow" id="theme-next" aria-label="Следующий стиль">▼</button>
                </div>
            </div>
        </div>`;
}

function bindThemeSwitcher(initialThemeId) {
    let currentIndex = getThemeIndex(initialThemeId);
    const radio = content.querySelector('input[name="app-theme"]');
    const dotsEl = document.getElementById("theme-switcher-dots");
    const labelEl = document.getElementById("theme-switcher-label");

    const updatePreview = index => {
        currentIndex = (index + THEMES.length) % THEMES.length;
        const theme = THEMES[currentIndex];

        if (radio) {
            radio.value = theme.id;
        }

        if (dotsEl) {
            dotsEl.innerHTML = theme.dots
                .map(color => `<span class="theme-dot" style="background:${color}"></span>`)
                .join("");
        }

        if (labelEl) {
            labelEl.textContent = theme.label;
        }

        applyTheme(theme.id);
    };

    document.getElementById("theme-prev")?.addEventListener("click", () => updatePreview(currentIndex - 1));
    document.getElementById("theme-next")?.addEventListener("click", () => updatePreview(currentIndex + 1));
}

function openAppSettings() {
    state.appSettingsReturnScreen = state.screen;
    state.screen = "app-settings";
    render();
}

function loadPreferences() {
    return {
        themeId: localStorage.getItem(PREF_KEYS.theme) || THEMES[0].id,
        displayName: localStorage.getItem(PREF_KEYS.displayName) || ""
    };
}

function savePreferences(prefs) {
    localStorage.setItem(PREF_KEYS.theme, prefs.themeId);
    localStorage.setItem(PREF_KEYS.displayName, prefs.displayName.trim());
}

function applyTheme(themeId) {
    const theme = THEMES.find(item => item.id === themeId) || THEMES[0];
    const root = document.documentElement;

    root.style.setProperty("--accent", theme.accent);
    root.style.setProperty("--accent-dark", theme.accentDark);
    root.style.setProperty("--accent-soft", theme.accentSoft);
    root.style.setProperty("--icon-active", theme.iconActive);
    root.style.setProperty("--icon-muted", theme.iconMuted);
    root.style.setProperty("--coral", theme.accent);
    root.style.setProperty("--coral-dark", theme.accentDark);
    root.style.setProperty("--teal", theme.teal);
    root.style.setProperty("--teal-dark", theme.tealDark);
    root.style.setProperty("--mint", theme.mint);
    root.style.setProperty("--home-btn", theme.accent);
    root.style.setProperty("--home-btn-active", theme.accentDark);
    root.dataset.theme = theme.id;
}

function getDisplayName() {
    const custom = loadPreferences().displayName.trim();
    return custom || state.menu?.displayName || "друг";
}

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
    const shell = document.querySelector(".app-shell");
    const appBg = tg?.themeParams?.secondary_bg_color || "#f3f6f8";

    if (tg) {
        tg.ready();
        tg.expand();
        shell.style.backgroundColor = appBg;
        return true;
    }

    if (isLocalDev()) {
        shell.style.backgroundColor = appBg;
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

    const text = await response.text();
    let payload = null;

    if (text) {
        try {
            payload = JSON.parse(text);
        } catch {
            payload = null;
        }
    }

    if (!response.ok) {
        const message = payload?.message || payload?.Message || text || `Ошибка ${response.status}`;
        const error = new Error(message);
        error.status = response.status;
        throw error;
    }

    return payload ?? {};
}

function resolveBankId(result) {
    return result?.bankId || result?.BankId || null;
}

function isNotFoundError(error) {
    return error?.status === 404;
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

async function redirectToProfile(clearLastBank = false) {
    if (clearLastBank) {
        state.selectedBankId = null;
    }

    state.bank = null;
    state.cardFlipped = false;
    state.creditMember = null;
    state.memberSearch = "";
    state.showAddCurrency = false;
    state.screen = "menu";
    applyMenuResponse(await apiPost("/api/menu"));

    if (state.selectedBankId && !state.menu.banks.some(bank => bank.id === state.selectedBankId)) {
        state.selectedBankId = null;
    }

    render();
}

async function loadMenu(clearLastBank = false) {
    await redirectToProfile(clearLastBank);
}

function resetTransactionsState() {
    state.transactionsExpanded = false;
    state.transactionsPage = [];
    state.transactionsHasMore = false;
    state.transactionsLoading = false;
}

async function loadTransactionsPage({ reset = false } = {}) {
    const bank = state.bank;
    if (!bank?.id) {
        return;
    }

    if (state.transactionsLoading) {
        return;
    }

    state.transactionsLoading = true;
    if (reset) {
        state.transactionsPage = [];
    }

    try {
        const skip = reset ? 0 : state.transactionsPage.length;
        const result = await apiPost(`/api/banks/${bank.id}/transactions`, { skip, limit: 10 });
        const items = result.transactions || result.Transactions || [];

        state.transactionsPage = reset ? items : [...state.transactionsPage, ...items];
        state.transactionsHasMore = Boolean(result.hasMore ?? result.HasMore);
        state.transactionsExpanded = true;
    } finally {
        state.transactionsLoading = false;
    }
}

async function loadBank(bankId) {
    try {
        state.bank = await apiPost(`/api/banks/${bankId}`);
    } catch (error) {
        if (isNotFoundError(error)) {
            await redirectToProfile(true);
            return;
        }

        throw error;
    }

    state.selectedBankId = bankId;
    resetTransactionsState();
    pruneCart(state.bank);
    state.screen = "bank";
    state.cardFlipped = false;
    render();
}

async function refreshBank() {
    if (!state.selectedBankId) {
        return;
    }

    try {
        await loadBank(state.selectedBankId);
    } catch (error) {
        if (!isNotFoundError(error)) {
            throw error;
        }
    }
}

function applyMenuResponse(menu) {
    state.menu = menu;
    state.unreadNotifications = menu?.unreadNotifications ?? menu?.UnreadNotifications ?? 0;
}

async function loadNotificationsPage({ reset = false } = {}) {
    if (state.notificationsLoading) {
        return;
    }

    state.notificationsLoading = true;
    if (reset) {
        state.notifications = [];
    }

    try {
        const skip = reset ? 0 : state.notifications.length;
        const result = await apiPost("/api/notifications", { skip, limit: 20 });
        const items = result.notifications || result.Notifications || [];

        state.notifications = reset ? items : [...state.notifications, ...items];
        state.notificationsHasMore = Boolean(result.hasMore ?? result.HasMore);
    } finally {
        state.notificationsLoading = false;
    }
}

async function openNotifications() {
    state.screen = "notifications";
    await loadNotificationsPage({ reset: true });

    try {
        await apiPost("/api/notifications/read");
        state.unreadNotifications = 0;
    } catch {
        // ignore read errors, list is still shown
    }

    render();
}

async function bootstrap() {
    applyMenuResponse(await apiPost("/api/menu"));
    if (state.menu.banks.length === 1) {
        await loadBank(state.menu.banks[0].id);
        return;
    }

    state.screen = "menu";
    render();
}

function updateNav() {
    const hasLastBank = Boolean(state.selectedBankId);
    navBanks.classList.toggle("active", state.screen === "menu");
    navHome.classList.toggle("active", state.screen === "bank");
    navHome.classList.toggle("available", hasLastBank);
    navHome.classList.toggle("disabled", !hasLastBank);
    navHome.disabled = !hasLastBank;
    navShop.classList.toggle("active", state.screen === "shop" || state.screen === "shop-settings");
    navShop.classList.toggle("available", hasLastBank);
    navShop.classList.toggle("disabled", !hasLastBank);
    navShop.disabled = !hasLastBank;
    navCart.classList.toggle("active", state.screen === "cart");
    navCart.classList.toggle("available", hasLastBank);
    navCart.classList.toggle("disabled", !hasLastBank);
    navCart.disabled = !hasLastBank;
    navSeparators.forEach(separator => separator?.classList.toggle("is-active", hasLastBank));
    syncCartBadge();
    syncNotificationBadge();
}

function render() {
    updateNav();

    if (state.screen === "menu") {
        renderMenu();
        return;
    }

    if (state.screen === "notifications") {
        renderNotifications();
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

    if (state.screen === "shop-settings") {
        renderShopSettings();
        return;
    }

    if (state.screen === "cart") {
        renderCart();
        return;
    }

    if (state.screen === "settings") {
        renderSettings();
        return;
    }

    if (state.screen === "app-settings") {
        renderAppSettings();
        return;
    }

    if (state.screen === "create-bank") {
        renderCreateBank();
        return;
    }

    if (state.screen === "credit-users") {
        renderCreditUsers();
        return;
    }

    if (state.screen === "credit-add") {
        renderCreditAdd();
        return;
    }

    if (state.screen === "fine-users") {
        renderFineUsers();
        return;
    }

    if (state.screen === "fine-add") {
        renderFineAdd();
    }
}

function filterMembers(members, query) {
    const normalized = query.trim().toLowerCase();
    if (!normalized) {
        return members;
    }

    return members.filter(member =>
        String(member.telegramUserId).includes(normalized)
        || member.displayName.toLowerCase().includes(normalized));
}

function formatMemberBalances(bank, balances) {
    return (bank.currencies || [])
        .map(currency => {
            const meta = getCurrencyMeta(bank, currency.code);
            const amount = balances?.[currency.code] ?? 0;
            return `${formatCurrencyIconText(meta.icon)} ${amount}`;
        })
        .join(" · ");
}

function renderMemberBalances(bank, balances) {
    const chips = (bank.currencies || [])
        .map(currency => {
            const meta = getCurrencyMeta(bank, currency.code);
            const amount = balances?.[currency.code] ?? 0;
            return `<span class="balance-chip">${renderCurrencyIconDisplay(meta.icon, "balance-chip__icon")}<span class="balance-chip__amount">${amount}</span></span>`;
        })
        .join("");

    return `<div class="balance-chips">${chips}</div>`;
}

function renderJoinField() {
    return `
        <div class="search-field join-field">
            <span class="search-field__icon">${icon("search", { size: 20 })}</span>
            <input
                id="join-code"
                type="text"
                placeholder="присоединиться по коду"
                autocomplete="off"
                spellcheck="false"
                maxlength="16">
            <button class="join-field__btn" id="join-submit" type="button">присоединиться</button>
        </div>`;
}

function renderCreateBankTrigger() {
    return `
        <button class="pill-button primary create-bank-trigger" id="open-create-bank" type="button">
            ${icon("add", { size: 18, color: "#fff" })}
            Создать банк
        </button>`;
}

function renderCreateBank() {
    content.innerHTML = `
        <button class="back-link" type="button" id="back-from-create-bank">← Назад</button>
        <h2 class="section-title">Создание банка</h2>
        <form class="create-bank-form panel" id="create-bank-form">
            <label class="form-label" for="create-bank-name">Название банка</label>
            <input
                id="create-bank-name"
                class="create-bank-form__input"
                type="text"
                name="name"
                placeholder="Например, Семейный банк"
                autocomplete="off"
                maxlength="128"
                required>

            <p class="form-label create-bank-form__currency-title">Первая валюта</p>
            <p class="hint create-bank-form__currency-hint">Эмодзи или картинка и название. Позже можно добавить ещё.</p>
            <div class="currency-row create-bank-form__currency">
                ${renderCurrencyIconPicker({ id: "create-currency", value: "⭐" })}
                <input
                    class="currency-row__name"
                    id="create-currency-name"
                    name="currencyName"
                    type="text"
                    maxlength="64"
                    placeholder="Название"
                    aria-label="Название валюты"
                    required>
            </div>

            <p class="create-bank-hint hidden" id="create-bank-error"></p>

            <button class="pill-button primary create-bank-form__submit" id="create-bank-submit" type="submit">
                ${icon("add", { size: 18, color: "#fff" })}
                Создать банк
            </button>
        </form>`;

    const form = document.getElementById("create-bank-form");
    const errorEl = document.getElementById("create-bank-error");
    const nameInput = document.getElementById("create-bank-name");
    const currencyNameInput = document.getElementById("create-currency-name");

    const showCreateError = message => {
        errorEl.textContent = message;
        errorEl.classList.remove("hidden");
    };

    const clearCreateError = () => {
        errorEl.textContent = "";
        errorEl.classList.add("hidden");
    };

    form.querySelectorAll("input").forEach(input => {
        input.addEventListener("input", clearCreateError);
    });

    bindCurrencyIconPickers(form);

    document.getElementById("back-from-create-bank").addEventListener("click", () => {
        state.screen = state.createBankReturnScreen || "menu";
        render();
    });

    form.addEventListener("submit", async event => {
        event.preventDefault();
        clearCreateError();

        if (!getInitData()) {
            showCreateError("Открой Mini App через Telegram");
            return;
        }

        const name = nameInput.value.trim();
        const currencyName = currencyNameInput.value.trim();
        const iconPicker = form.querySelector(".currency-icon-picker");
        const iconData = readCurrencyIconPicker(iconPicker);
        const code = buildCurrencyCode(currencyName);
        const fallbackIcon = iconData.type === "emoji" ? iconData.value : "⭐";

        if (!name) {
            showCreateError("Введи название банка");
            nameInput.focus();
            return;
        }

        if (!currencyName) {
            showCreateError("Укажи название валюты");
            return;
        }

        const submitButton = document.getElementById("create-bank-submit");
        const defaultLabel = submitButton.innerHTML;
        submitButton.disabled = true;
        submitButton.textContent = "создаём…";

        try {
            const result = await apiPost("/api/banks/create", {
                name,
                currencies: [{ code, name: currencyName, icon: fallbackIcon }]
            });
            const bankId = resolveBankId(result);
            if (!bankId) {
                throw new Error("Сервер не вернул идентификатор банка.");
            }

            if (iconData.type === "file") {
                await uploadCurrencyIcon(bankId, code, iconData.file);
            }

            applyMenuResponse(await apiPost("/api/menu"));
            await loadBank(bankId);
        } catch (error) {
            showCreateError(error.message || "Не удалось создать банк");
        } finally {
            submitButton.disabled = false;
            submitButton.innerHTML = defaultLabel;
        }
    });
}

function bindCreateBankTrigger() {
    const button = document.getElementById("open-create-bank");
    if (!button) {
        return;
    }

    button.addEventListener("click", () => {
        state.createBankReturnScreen = state.screen;
        state.screen = "create-bank";
        render();
    });
}

function buildCurrencyCode(name) {
    const transliterated = name
        .trim()
        .toLowerCase()
        .replaceAll("ё", "e")
        .replace(/[а-я]/g, char => ({
            а: "a", б: "b", в: "v", г: "g", д: "d", е: "e", ж: "zh", з: "z", и: "i", й: "y",
            к: "k", л: "l", м: "m", н: "n", о: "o", п: "p", р: "r", с: "s", т: "t", у: "u",
            ф: "f", х: "h", ц: "ts", ч: "ch", ш: "sh", щ: "sch", ъ: "", ы: "y", ь: "",
            э: "e", ю: "yu", я: "ya"
        })[char] || "")
        .replace(/[^a-z0-9_-]+/g, "_")
        .replace(/^_+|_+$/g, "")
        .slice(0, 32);

    if (transliterated.length >= 2) {
        return transliterated;
    }

    let hash = 0;
    for (const char of name.trim()) {
        hash = ((hash << 5) - hash + char.charCodeAt(0)) | 0;
    }

    return `cur_${Math.abs(hash).toString(36)}`.slice(0, 32);
}

function bindJoinForm() {
    const input = document.getElementById("join-code");
    const button = document.getElementById("join-submit");
    if (!input || !button) {
        return;
    }

    const submitJoin = async () => {
        const code = input.value.trim();
        if (!code) {
            return;
        }

        input.disabled = true;
        button.disabled = true;
        try {
            const result = await apiPost("/api/banks/join", { code });
            const bankId = resolveBankId(result);
            if (!bankId) {
                throw new Error("Сервер не вернул идентификатор банка.");
            }

            applyMenuResponse(await apiPost("/api/menu"));
            await loadBank(bankId);
        } catch (error) {
            showError(error);
        } finally {
            input.disabled = false;
            button.disabled = false;
        }
    };

    button.addEventListener("click", () => submitJoin());
    input.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            submitJoin();
        }
    });
}

function renderAppSettings() {
    const prefs = loadPreferences();
    const defaultName = state.menu?.displayName || "";

    content.innerHTML = `
        <button class="back-link" type="button" id="back-from-app-settings">← Назад</button>
        <h2 class="section-title">Настройки</h2>
        <div class="app-settings">
            <label class="form-label" for="app-display-name">Ваше имя</label>
            <div class="settings-field">
                <input
                    id="app-display-name"
                    type="text"
                    value="${escapeHtml(prefs.displayName || defaultName)}"
                    placeholder="Как к вам обращаться"
                    autocomplete="name"
                    maxlength="48">
            </div>
            <p class="form-label">Основной стиль</p>
            ${renderThemeSwitcher(prefs.themeId)}
            <button class="pill-button primary" id="save-app-settings" type="button">Сохранить</button>
        </div>`;

    const returnScreen = () => {
        state.screen = state.appSettingsReturnScreen || (state.bank ? "bank" : "menu");
        render();
    };

    document.getElementById("back-from-app-settings").addEventListener("click", () => {
        applyTheme(loadPreferences().themeId);
        returnScreen();
    });

    bindThemeSwitcher(prefs.themeId);

    document.getElementById("save-app-settings").addEventListener("click", () => {
        const themeId = content.querySelector('input[name="app-theme"]:checked')?.value || THEMES[0].id;
        const displayName = document.getElementById("app-display-name").value;
        savePreferences({ themeId, displayName });
        applyTheme(themeId);
        returnScreen();
    });
}

function renderNotifications() {
    content.innerHTML = `
        <button class="back-link" id="back-from-notifications" type="button">← Назад</button>
        ${renderPageHeader({ title: "Уведомления" })}
        <div class="panel notifications-panel">
            ${state.notifications.length === 0 && !state.notificationsLoading
                ? '<p class="empty">Уведомлений пока нет</p>'
                : state.notifications.map(item => `
                    <div class="notification-item ${item.isRead || item.IsRead ? "" : "notification-item--unread"}">
                        <div class="notification-item__head">
                            <strong>${escapeHtml(item.title || item.Title)}</strong>
                            <span class="hint">${formatDate(item.createdAt || item.CreatedAt)}</span>
                        </div>
                        <div class="notification-item__text">${escapeHtml(item.message || item.Message)}</div>
                    </div>`).join("")}
            ${state.notificationsHasMore
                ? `<button class="pill-button notifications-more-btn" id="load-more-notifications" type="button" ${state.notificationsLoading ? "disabled" : ""}>
                    ${state.notificationsLoading ? "Загружаем…" : "Показать больше"}
                </button>`
                : ""}
        </div>`;

    document.getElementById("back-from-notifications").addEventListener("click", () => {
        state.screen = state.selectedBankId ? "bank" : "menu";
        render();
    });

    const loadMoreButton = document.getElementById("load-more-notifications");
    if (loadMoreButton) {
        loadMoreButton.addEventListener("click", async () => {
            try {
                await loadNotificationsPage();
                render();
            } catch (error) {
                showError(error);
            }
        });
    }
}

function renderMenu() {
    const banks = state.menu?.banks || [];
    const name = getDisplayName();

    if (banks.length === 0) {
        content.innerHTML = `
            ${renderPageHeader({
                greeting: `Привет, ${escapeHtml(name)}`,
                showNotifications: true
            })}
            ${renderJoinField()}
            ${renderCreateBankTrigger()}`;
        bindJoinForm();
        bindCreateBankTrigger();
        return;
    }

    content.innerHTML = `
        ${renderPageHeader({
            greeting: `Привет, ${escapeHtml(name)}`,
            title: "Выбери банк",
            showNotifications: true
        })}
        ${renderJoinField()}
        <div class="bank-pick">
            ${banks.map(bank => `
                <button class="bank-pick-card" data-bank-id="${bank.id}">
                    <span class="bank-pick-card__head">
                        ${bank.isOwner
                            ? icon("crown", { size: 20, color: "#ffd34d" })
                            : icon("profile", { size: 20, color: "rgba(255,255,255,0.9)" })}
                        <strong>${escapeHtml(bank.name)}</strong>
                    </span>
                    <span>${bank.isOwner ? "Создатель банка" : "Участник"}</span>
                </button>`).join("")}
        </div>`;

    bindJoinForm();

    content.querySelectorAll("[data-bank-id]").forEach(button => {
        button.addEventListener("click", () => loadBank(button.dataset.bankId).catch(showError));
    });
}

function renderCardBalances(bank, compact = false) {
    const currencies = (bank.currencies || []).slice(0, bank.maxCurrencies || 4);

    if (compact) {
        return currencies.map(currency => {
            const meta = getCurrencyMeta(bank, currency.code);
            const amount = bank.balances?.[currency.code] ?? 0;
            return `
                <div class="card-back-balance-row">
                    <span class="card-back-balance-icon">${renderCurrencyIconDisplay(meta.icon)} ${escapeHtml(meta.name)}</span>
                    <strong>${amount}</strong>
                </div>`;
        }).join("");
    }

    const maxBalance = Math.max(10, ...currencies.map(c => bank.balances?.[c.code] ?? 0));

    return currencies.map(currency => {
        const meta = getCurrencyMeta(bank, currency.code);
        const amount = bank.balances?.[currency.code] ?? 0;
        const width = Math.min(100, (amount / maxBalance) * 100);
        return `
            <div class="debt-row">
                <div class="debt-head">
                    <div class="debt-label">
                        <div class="debt-icon debt-icon--${meta.bar}">${renderCurrencyIconDisplay(meta.icon, "debt-icon__graphic")}</div>
                        <span>${escapeHtml(meta.name)}</span>
                    </div>
                    <strong>${amount}</strong>
                </div>
                <div class="debt-bar">
                    <div class="debt-bar-fill ${meta.bar}" style="width:${width}%"></div>
                </div>
            </div>`;
    }).join("");
}

function renderBank() {
    const bank = state.bank;
    if (!bank) {
        return;
    }

    const roleLabel = bank.isOwner ? "Owner" : "Member";
    const cardBackground = bank.cardImageUrl
        ? `<img src="${bank.cardImageUrl}?t=${Date.now()}" alt="Карта">`
        : `<div class="credit-card-pattern"></div>`;
    const cardInner = `${cardBackground}<div class="credit-card-chip" aria-hidden="true"></div>`;

    const recentTransactions = bank.transactions || [];
    const hasMoreTransactions = Boolean(bank.hasMoreTransactions ?? bank.HasMoreTransactions);

    content.innerHTML = `
        <div class="card-carousel">
            <div class="card-flip ${state.cardFlipped ? "is-flipped" : ""}" id="card-flip">
                <div class="card-flip-inner">
                    <div class="card-flip-front">
                        <div class="credit-card ${bank.cardImageUrl ? "has-image" : ""}">
                            ${cardInner}
                            <div class="credit-card-top">${escapeHtml(bank.name)}</div>
                            <div class="credit-card-bottom">${roleLabel}</div>
                        </div>
                    </div>
                    <div class="card-flip-back">
                        <div>
                            <div class="card-back-label">Имя</div>
                            <input
                                id="card-holder-name"
                                class="card-back-name"
                                type="text"
                                maxlength="64"
                                value="${escapeHtml(bank.holderName || "")}"
                                placeholder="Введи имя"
                                autocomplete="name">
                        </div>
                        <div>
                            <div class="card-back-label">Номер карты</div>
                            <div class="card-back-number">${escapeHtml(bank.cardNumber || "")}</div>
                        </div>
                        <div class="card-back-balances">
                            ${renderCardBalances(bank, true)}
                        </div>
                        <button class="card-back-reset" id="reset-card-skin" type="button" ${bank.hasCustomSkin ? "" : "disabled"}>
                            Сбросить шкурку
                        </button>
                    </div>
                </div>
            </div>
            <div class="card-actions">
                <button class="pill-button" id="change-card-photo">
                    ${icon("add", { size: 18, color: "var(--teal-dark)" })}
                    Сменить фото
                </button>
                ${bank.canManage ? `<button class="pill-button primary" id="open-settings">${icon("settings", { size: 18, color: "#fff" })}Управление</button>` : ""}
            </div>
        </div>

        <div class="panel">
            <h2 class="section-title">Текущий баланс</h2>
            ${renderCardBalances(bank)}
        </div>

        <div class="panel">
            <h2 class="section-title">Последние операции</h2>
            ${recentTransactions.length === 0
                ? '<p class="empty">Операций пока нет</p>'
                : recentTransactions.map(tx => renderTransactionItem(tx, bank)).join("")}
            ${recentTransactions.length > 0 && hasMoreTransactions && !state.transactionsExpanded
                ? `<button class="pill-button transactions-toggle-btn" id="toggle-all-transactions" type="button" ${state.transactionsLoading ? "disabled" : ""}>
                    ${state.transactionsLoading ? "Загружаем…" : "Все операции"}
                </button>`
                : ""}
        </div>

        ${state.transactionsExpanded ? `
        <div class="panel">
            <h2 class="section-title">Все операции</h2>
            ${state.transactionsPage.length === 0 && !state.transactionsLoading
                ? '<p class="empty">Операций пока нет</p>'
                : state.transactionsPage.map(tx => renderTransactionItem(tx, bank)).join("")}
            ${state.transactionsHasMore
                ? `<button class="pill-button transactions-more-btn" id="load-more-transactions" type="button" ${state.transactionsLoading ? "disabled" : ""}>
                    ${state.transactionsLoading ? "Загружаем…" : "Показать больше"}
                </button>`
                : ""}
        </div>` : ""}`;

    document.getElementById("change-card-photo").addEventListener("click", () => fileMyCard.click());

    const toggleAllTransactions = document.getElementById("toggle-all-transactions");
    if (toggleAllTransactions) {
        toggleAllTransactions.addEventListener("click", async () => {
            try {
                await loadTransactionsPage({ reset: true });
                render();
            } catch (error) {
                showError(error);
            }
        });
    }

    const loadMoreTransactions = document.getElementById("load-more-transactions");
    if (loadMoreTransactions) {
        loadMoreTransactions.addEventListener("click", async () => {
            try {
                await loadTransactionsPage();
                render();
            } catch (error) {
                showError(error);
            }
        });
    }

    const cardFlip = document.getElementById("card-flip");
    cardFlip.addEventListener("click", event => {
        if (event.target.closest("input, button, .card-back-reset")) {
            return;
        }

        state.cardFlipped = !state.cardFlipped;
        cardFlip.classList.toggle("is-flipped", state.cardFlipped);
    });

    const holderInput = document.getElementById("card-holder-name");
    holderInput.addEventListener("click", event => event.stopPropagation());
    holderInput.addEventListener("keydown", event => event.stopPropagation());
    holderInput.addEventListener("blur", async () => {
        const nextName = holderInput.value.trim();
        if (!nextName || nextName === (bank.holderName || "").trim()) {
            return;
        }

        try {
            await apiPost(`/api/banks/${bank.id}/card-holder-name`, { holderName: nextName });
            await refreshBank();
        } catch (error) {
            showError(error);
        }
    });

    const resetSkinButton = document.getElementById("reset-card-skin");
    resetSkinButton.addEventListener("click", async event => {
        event.stopPropagation();
        if (!bank.hasCustomSkin) {
            return;
        }

        try {
            await apiPost(`/api/banks/${bank.id}/reset-card-image`);
            state.cardFlipped = true;
            await refreshBank();
        } catch (error) {
            showError(error);
        }
    });

    const settingsButton = document.getElementById("open-settings");
    if (settingsButton) {
        settingsButton.addEventListener("click", () => {
            state.inviteCode = null;
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
        ${bank.canManage ? `
            <div class="card-actions shop-actions">
                <button class="pill-button primary" id="open-shop-settings" type="button">
                    ${icon("settings", { size: 18, color: "#fff" })}
                    Управление
                </button>
            </div>` : ""}
        <div class="shop-grid">
            ${bank.products.length === 0
                ? `<div class="panel empty-state">
                        ${icon("empty-cart", { bg: true, size: 36, bgColor: "var(--mint)", color: "var(--teal-dark)" })}
                        <strong>В магазине пока пусто</strong>
                        <p class="hint">${bank.canManage ? "Добавь первый товар через «Управление»." : "Создатель банка ещё не добавил товары."}</p>
                   </div>`
                : bank.products.map(product => {
                    const meta = getCurrencyMeta(bank, product.currencyCode);
                    return `
                        <div class="product-card">
                            <div class="product-card__head">
                                <strong>${escapeHtml(product.name)}</strong>
                                ${bank.canManage ? `
                                    <button
                                        class="product-card__delete"
                                        type="button"
                                        data-delete-product="${product.id}"
                                        data-product-name="${escapeHtml(product.name)}"
                                        aria-label="Удалить ${escapeHtml(product.name)}">
                                        ${icon("delete", { size: 18, color: "var(--muted)" })}
                                    </button>` : ""}
                            </div>
                            ${product.description ? `<p class="product-card__description hint">${escapeHtml(product.description)}</p>` : ""}
                            <div class="product-card__footer">
                                <div class="product-price">${product.price} ${escapeHtml(meta.name)}</div>
                                <button
                                    class="product-card__add"
                                    type="button"
                                    data-add-to-cart="${product.id}"
                                    aria-label="Добавить ${escapeHtml(product.name)} в корзину">
                                    ${icon("add", { size: 18, color: "var(--teal-dark)" })}
                                </button>
                            </div>
                        </div>`;
                }).join("")}
        </div>
        ${bank.canManage ? `
            <div class="modal-overlay hidden" id="delete-product-modal">
                <div class="modal-card" role="alertdialog" aria-modal="true">
                    <div class="modal-icon">${icon("attention", { bg: true, size: 32, bgColor: "var(--danger-soft)", color: "var(--danger-dark)" })}</div>
                    <h3 class="modal-title modal-title--danger" id="delete-product-title">Удалить товар?</h3>
                    <p class="modal-text">Товар исчезнет из магазина. Это действие нельзя отменить.</p>
                    <div class="modal-actions">
                        <button class="modal-btn modal-btn--cancel" id="cancel-delete-product" type="button">Отмена</button>
                        <button class="modal-btn modal-btn--danger" id="confirm-delete-product" type="button">Удалить</button>
                    </div>
                </div>
            </div>` : ""}`;

    const shopSettingsButton = document.getElementById("open-shop-settings");
    if (shopSettingsButton) {
        shopSettingsButton.addEventListener("click", () => {
            state.screen = "shop-settings";
            render();
        });
    }

    content.querySelectorAll("[data-add-to-cart]").forEach(button => {
        button.addEventListener("click", () => {
            const product = bank.products.find(item => item.id === button.dataset.addToCart);
            if (!product) {
                return;
            }

            addProductToCart(bank, product);
            if (tg?.HapticFeedback?.impactOccurred) {
                tg.HapticFeedback.impactOccurred("light");
            }
        });
    });

    if (bank.canManage) {
        setupDeleteProduct(bank);
    }
}

function renderCart() {
    const bank = state.bank;
    if (!bank) {
        return;
    }

    const items = loadCartItems(bank.id);
    const totals = items.reduce((acc, item) => {
        acc[item.currencyCode] = (acc[item.currencyCode] || 0) + item.price * item.quantity;
        return acc;
    }, {});

    content.innerHTML = `
        <h2 class="section-title">Корзина</h2>
        ${items.length === 0
            ? `<div class="panel empty-state">
                    ${icon("empty-cart", { bg: true, size: 36, bgColor: "var(--mint)", color: "var(--teal-dark)" })}
                    <strong>Корзина пуста</strong>
                    <p class="hint">Добавь товары из магазина.</p>
               </div>`
            : `
                <div class="cart-list">
                    ${items.map(item => {
                        const meta = getCurrencyMeta(bank, item.currencyCode);
                        return `
                            <div class="cart-item" data-cart-item="${item.productId}">
                                <div class="cart-item__info">
                                    <strong>${escapeHtml(item.name)}</strong>
                                    ${item.description ? `<div class="hint cart-item__description">${escapeHtml(item.description)}</div>` : ""}
                                    <div class="hint">${item.price} ${escapeHtml(meta.name)}</div>
                                </div>
                                <div class="cart-item__controls">
                                    <button class="cart-item__qty" type="button" data-cart-dec="${item.productId}" aria-label="Уменьшить">−</button>
                                    <span class="cart-item__count">${item.quantity}</span>
                                    <button class="cart-item__qty" type="button" data-cart-inc="${item.productId}" aria-label="Увеличить">+</button>
                                    <button class="cart-item__remove" type="button" data-cart-remove="${item.productId}" aria-label="Убрать">
                                        ${icon("delete", { size: 16, color: "var(--muted)" })}
                                    </button>
                                </div>
                            </div>`;
                    }).join("")}
                </div>
                <div class="panel cart-summary">
                    <h3 class="cart-summary__title">Итого</h3>
                    ${Object.entries(totals).map(([code, amount]) => {
                        const meta = getCurrencyMeta(bank, code);
                        return `<div class="cart-summary__row">${amount} ${escapeHtml(meta.name)}</div>`;
                    }).join("")}
                </div>
                <button class="submit-button" id="checkout-cart" type="button">
                    ${icon("cart", { size: 18, color: "#fff" })}
                    Купить
                </button>`}`;

    content.querySelectorAll("[data-cart-inc]").forEach(button => {
        button.addEventListener("click", () => {
            updateCartItemQuantity(bank.id, button.dataset.cartInc, 1);
            renderCart();
        });
    });

    content.querySelectorAll("[data-cart-dec]").forEach(button => {
        button.addEventListener("click", () => {
            updateCartItemQuantity(bank.id, button.dataset.cartDec, -1);
            renderCart();
        });
    });

    content.querySelectorAll("[data-cart-remove]").forEach(button => {
        button.addEventListener("click", () => {
            removeCartItem(bank.id, button.dataset.cartRemove);
            renderCart();
        });
    });

    const checkoutButton = document.getElementById("checkout-cart");
    if (checkoutButton) {
        checkoutButton.addEventListener("click", async () => {
            const cartItems = loadCartItems(bank.id);
            if (cartItems.length === 0) {
                return;
            }

            checkoutButton.disabled = true;
            checkoutButton.textContent = "Покупаем…";
            try {
                await apiPost(`/api/banks/${bank.id}/buy`, {
                    productIds: expandCartProductIds(cartItems)
                });
                clearCart(bank.id);
                await refreshBank();
                state.screen = "bank";
                render();
            } catch (error) {
                checkoutButton.disabled = false;
                checkoutButton.innerHTML = `${icon("cart", { size: 18, color: "#fff" })} Купить`;
                showError(error);
            }
        });
    }
}

function renderShopSettings() {
    const bank = state.bank;
    if (!bank?.canManage) {
        content.innerHTML = '<p class="hint">Управление магазином доступно только создателю банка.</p>';
        return;
    }

    content.innerHTML = `
        <button class="back-link" id="back-to-shop" type="button">← Магазин</button>
        <h2 class="section-title">Управление магазином</h2>

        <div class="panel panel--stack">
            <h2 class="section-title">Новый товар</h2>
            <form class="form-card" id="add-product-form">
                <input name="name" placeholder="Название" required maxlength="160">
                <textarea name="description" placeholder="Описание (необязательно)" maxlength="512" rows="3"></textarea>
                ${renderCurrencySelect(bank, { id: "product-currency-select" })}
                <input name="price" type="number" min="1" step="1" placeholder="Цена" required>
                <button class="submit-button" type="submit">
                    ${icon("add", { size: 18, color: "#fff" })}
                    Добавить в магазин
                </button>
            </form>
        </div>`;

    bindCurrencySelects(content);

    document.getElementById("back-to-shop").addEventListener("click", () => {
        state.screen = "shop";
        render();
    });

    document.getElementById("add-product-form").addEventListener("submit", async event => {
        event.preventDefault();
        const data = new FormData(event.target);
        try {
            const description = data.get("description")?.toString().trim();
            await apiPost(`/api/banks/${bank.id}/products`, {
                name: data.get("name"),
                description: description || null,
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

function renderSettings() {
    const bank = state.bank;
    if (!bank?.canManage) {
        content.innerHTML = '<p class="hint">Настройки доступны только создателю банка.</p>';
        return;
    }

    content.innerHTML = `
        <h2 class="section-title">Управление банком</h2>

        <div class="panel compact-panel">
            <div class="compact-panel__text">
                <span class="compact-panel__title">Баланс карт</span>
                <span class="compact-panel__hint">Начислить валюту участнику.</span>
            </div>
            <button class="pill-button primary compact-panel__btn" id="open-credit-users" type="button">
                ${icon("profile", { size: 16, color: "#fff" })}
                Баланс
            </button>
        </div>

        <div class="panel compact-panel">
            <div class="compact-panel__text">
                <span class="compact-panel__title">Штрафы</span>
                <span class="compact-panel__hint">Списать валюту с карты участника.</span>
            </div>
            <button class="pill-button danger compact-panel__btn" id="open-fine-users" type="button">
                ${icon("attention", { size: 16, color: "var(--danger)" })}
                Штраф
            </button>
        </div>

        <div class="panel invite-panel">
            <div class="invite-panel__head">
                <div class="compact-panel__text">
                    <span class="compact-panel__title">Код приглашения</span>
                    <span class="compact-panel__hint">Поделись кодом, чтобы пригласить участника.</span>
                </div>
                <button class="pill-button primary compact-panel__btn" id="create-invite-btn" type="button">
                    ${icon("add", { size: 16, color: "#fff" })}
                    Создать
                </button>
            </div>
            ${state.inviteCode ? `
            <div class="invite-result">
                <button class="invite-code" id="copy-invite-btn" type="button" title="Скопировать">
                    <span class="invite-code__value">${escapeHtml(state.inviteCode.code)}</span>
                    ${icon("copy", { size: 16, color: "var(--text-muted, #888)" })}
                </button>
                <span class="compact-panel__hint">Действует до ${escapeHtml(formatDate(state.inviteCode.expiresAt))}</span>
            </div>` : ""}
        </div>

        <div class="panel compact-panel">
            <div class="compact-panel__text">
                <span class="compact-panel__title">Шаблон карты</span>
                <span class="compact-panel__hint">Картинка для новых карт.</span>
            </div>
            <button class="pill-button primary compact-panel__btn" id="upload-template-btn" type="button">
                ${icon("add", { size: 16, color: "#fff" })}
                Загрузить
            </button>
        </div>

        <div class="panel currencies-panel">
            <div class="currencies-panel__head">
                <span class="compact-panel__title">Валюты</span>
                <span class="compact-panel__hint">Эмодзи или картинка (до ${bank.maxCurrencies || 4})</span>
            </div>
            <div class="currency-list">
                ${(bank.currencies || []).map(currency => `
                    <div class="currency-row" data-currency-row="${currency.code}">
                        ${renderCurrencyIconPicker({
                            id: `currency-${currency.code}`,
                            value: resolveCurrencyIcon(bank, currency),
                            currencyCode: currency.code
                        })}
                        <input
                            class="currency-row__name"
                            type="text"
                            maxlength="64"
                            value="${escapeHtml(currency.name)}"
                            data-currency-code="${currency.code}"
                            placeholder="Название"
                            aria-label="Название ${escapeHtml(currency.name)}">
                        <button class="pill-button pill-button--sm" type="button" data-save-currency="${currency.code}">Сохр.</button>
                    </div>`).join("")}
            </div>
            ${(bank.currencies || []).length < (bank.maxCurrencies || 4)
                ? (state.showAddCurrency
                    ? `<form class="currency-add-form" id="add-currency-form">
                            ${renderCurrencyIconPicker({ id: "add-currency-icon", value: "⭐" })}
                            <input class="currency-row__name" name="name" type="text" placeholder="Название" required maxlength="64">
                            <button class="pill-button primary pill-button--sm" type="submit">Добавить</button>
                       </form>`
                    : `<button class="pill-button primary pill-button--sm add-currency-trigger" id="toggle-add-currency" type="button">
                            ${icon("add", { size: 16, color: "#fff" })}
                            Добавить
                       </button>`)
                : '<p class="currencies-panel__limit">Лимит валют.</p>'}
        </div>

        <div class="panel compact-panel danger-panel">
            <div class="compact-panel__text">
                <span class="compact-panel__title">Опасная зона</span>
                <span class="compact-panel__hint">Необратимо: карты, балансы, история.</span>
            </div>
            <button class="pill-button danger compact-panel__btn" id="open-delete-bank" type="button">
                ${icon("delete", { size: 16, color: "var(--danger)" })}
                Удалить
            </button>
        </div>

        <div class="modal-overlay hidden" id="delete-bank-modal">
            <div class="modal-card" role="alertdialog" aria-modal="true">
                <div class="modal-icon">${icon("attention", { bg: true, size: 32, bgColor: "var(--danger-soft)", color: "var(--danger-dark)" })}</div>
                <h3 class="modal-title modal-title--danger">Удалить «${escapeHtml(bank.name)}»?</h3>
                <p class="modal-text" id="delete-bank-text">Действие нельзя отменить. Подтверждение будет доступно через 5 сек.</p>
                <div class="modal-actions">
                    <button class="modal-btn modal-btn--cancel" id="cancel-delete-bank" type="button">Отмена</button>
                    <button class="modal-btn modal-btn--danger" id="confirm-delete-bank" type="button" disabled>
                        <span id="confirm-delete-label">Удалить (5)</span>
                    </button>
                </div>
            </div>
        </div>`;

    document.getElementById("open-credit-users").addEventListener("click", () => {
        state.creditMember = null;
        state.fineMember = null;
        state.memberSearch = "";
        state.screen = "credit-users";
        render();
    });

    document.getElementById("open-fine-users").addEventListener("click", () => {
        state.fineMember = null;
        state.creditMember = null;
        state.memberSearch = "";
        state.screen = "fine-users";
        render();
    });

    document.getElementById("upload-template-btn").addEventListener("click", () => fileTemplate.click());

    const createInviteBtn = document.getElementById("create-invite-btn");
    if (createInviteBtn) {
        createInviteBtn.addEventListener("click", async () => {
            createInviteBtn.disabled = true;
            try {
                const result = await apiPost(`/api/banks/${bank.id}/invite`);
                state.inviteCode = {
                    code: result.code || result.Code,
                    expiresAt: result.expiresAt || result.ExpiresAt
                };
                renderSettings();
            } catch (error) {
                createInviteBtn.disabled = false;
                showError(error);
            }
        });
    }

    const copyInviteBtn = document.getElementById("copy-invite-btn");
    if (copyInviteBtn) {
        copyInviteBtn.addEventListener("click", async () => {
            const code = state.inviteCode?.code;
            if (!code) {
                return;
            }

            try {
                await navigator.clipboard.writeText(code);
            } catch {
                const range = document.createElement("textarea");
                range.value = code;
                document.body.appendChild(range);
                range.select();
                document.execCommand("copy");
                range.remove();
            }

            if (tg?.HapticFeedback?.notificationOccurred) {
                tg.HapticFeedback.notificationOccurred("success");
            }

            copyInviteBtn.classList.add("invite-code--copied");
            setTimeout(() => copyInviteBtn.classList.remove("invite-code--copied"), 1200);
        });
    }

    bindCurrencyIconPickers(content);

    const toggleAddCurrency = document.getElementById("toggle-add-currency");
    if (toggleAddCurrency) {
        toggleAddCurrency.addEventListener("click", () => {
            state.showAddCurrency = true;
            renderSettings();
        });
    }

    const addCurrencyForm = document.getElementById("add-currency-form");
    if (addCurrencyForm) {
        addCurrencyForm.addEventListener("submit", async event => {
            event.preventDefault();
            const data = new FormData(event.target);
            const name = data.get("name")?.toString().trim();
            if (!name) {
                return;
            }

            const iconPicker = addCurrencyForm.querySelector(".currency-icon-picker");
            const iconData = iconPicker ? readCurrencyIconPicker(iconPicker) : { type: "emoji", value: "⭐" };
            const code = buildCurrencyCode(name);
            const fallbackIcon = iconData.type === "url" ? iconData.url : (iconData.type === "emoji" ? iconData.value : "⭐");

            try {
                await apiPost(`/api/banks/${bank.id}/currencies`, {
                    code,
                    name,
                    icon: fallbackIcon
                });

                if (iconData.type === "file") {
                    await uploadCurrencyIcon(bank.id, code, iconData.file);
                }

                state.showAddCurrency = false;
                await refreshBank();
                state.screen = "settings";
                render();
            } catch (error) {
                showError(error);
            }
        });
    }

    content.querySelectorAll("[data-save-currency]").forEach(button => {
        button.addEventListener("click", async () => {
            const code = button.dataset.saveCurrency;
            const row = content.querySelector(`[data-currency-row="${code}"]`);
            const nameInput = row?.querySelector(`.currency-row__name[data-currency-code="${code}"]`);
            const iconPicker = row?.querySelector(".currency-icon-picker");
            const nextName = nameInput?.value.trim();
            const iconData = iconPicker ? readCurrencyIconPicker(iconPicker) : null;
            if (!nextName || !iconData) {
                return;
            }

            try {
                if (iconData.type === "file") {
                    await uploadCurrencyIcon(bank.id, code, iconData.file);
                } else {
                    const nextIcon = iconData.type === "url" ? iconData.url : iconData.value;
                    await apiPost(`/api/banks/${bank.id}/currency`, {
                        currencyCode: code,
                        name: nextName,
                        icon: nextIcon
                    });
                }

                await refreshBank();
                state.screen = "settings";
                render();
            } catch (error) {
                showError(error);
            }
        });
    });

    setupDeleteBank(bank);
}

function setupDeleteProduct(bank) {
    const modal = document.getElementById("delete-product-modal");
    const title = document.getElementById("delete-product-title");
    const cancelButton = document.getElementById("cancel-delete-product");
    const confirmButton = document.getElementById("confirm-delete-product");
    if (!modal || !title || !cancelButton || !confirmButton) {
        return;
    }

    let pendingProductId = null;

    const closeModal = () => {
        pendingProductId = null;
        modal.classList.add("hidden");
        confirmButton.disabled = false;
        confirmButton.textContent = "Удалить";
    };

    content.querySelectorAll("[data-delete-product]").forEach(button => {
        button.addEventListener("click", event => {
            event.stopPropagation();
            pendingProductId = button.dataset.deleteProduct;
            title.textContent = `Удалить «${button.dataset.productName}»?`;
            modal.classList.remove("hidden");
        });
    });

    cancelButton.addEventListener("click", closeModal);
    modal.addEventListener("click", event => {
        if (event.target === modal) {
            closeModal();
        }
    });

    confirmButton.addEventListener("click", async () => {
        if (!pendingProductId) {
            return;
        }

        confirmButton.disabled = true;
        confirmButton.textContent = "Удаляем…";
        try {
            await apiPost(`/api/banks/${bank.id}/products/delete`, { productId: pendingProductId });
            closeModal();
            await refreshBank();
            state.screen = "shop";
            render();
        } catch (error) {
            closeModal();
            showError(error);
        }
    });
}

function setupDeleteBank(bank) {
    const modal = document.getElementById("delete-bank-modal");
    const openButton = document.getElementById("open-delete-bank");
    const cancelButton = document.getElementById("cancel-delete-bank");
    const confirmButton = document.getElementById("confirm-delete-bank");
    const confirmLabel = document.getElementById("confirm-delete-label");
    if (!modal || !openButton || !cancelButton || !confirmButton || !confirmLabel) {
        return;
    }

    let countdownTimer = null;

    const closeModal = () => {
        if (countdownTimer) {
            clearInterval(countdownTimer);
            countdownTimer = null;
        }
        modal.classList.add("hidden");
        confirmButton.disabled = true;
    };

    const startCountdown = () => {
        let remaining = 5;
        confirmButton.disabled = true;
        confirmLabel.textContent = `Удалить (${remaining})`;
        countdownTimer = setInterval(() => {
            remaining -= 1;
            if (remaining > 0) {
                confirmLabel.textContent = `Удалить (${remaining})`;
                return;
            }

            clearInterval(countdownTimer);
            countdownTimer = null;
            confirmButton.disabled = false;
            confirmLabel.textContent = "Удалить";
        }, 1000);
    };

    openButton.addEventListener("click", () => {
        modal.classList.remove("hidden");
        startCountdown();
    });

    cancelButton.addEventListener("click", closeModal);
    modal.addEventListener("click", event => {
        if (event.target === modal) {
            closeModal();
        }
    });

    confirmButton.addEventListener("click", async () => {
        if (confirmButton.disabled) {
            return;
        }

        confirmButton.disabled = true;
        confirmLabel.textContent = "Удаляем…";
        try {
            await apiPost("/api/banks/delete");
            closeModal();
            await redirectToProfile(true);
        } catch (error) {
            closeModal();
            if (isNotFoundError(error)) {
                await redirectToProfile(true);
                return;
            }

            showError(error);
        }
    });
}

function renderCreditUsers() {
    const bank = state.bank;
    if (!bank?.canManage) {
        content.innerHTML = '<p class="hint">Начисление доступно только создателю банка.</p>';
        return;
    }

    const members = filterMembers(bank.members || [], state.memberSearch);

    content.innerHTML = `
        <button class="back-link" id="back-to-settings" type="button">← Управление</button>
        <h2 class="section-title">Участники</h2>

        <div class="search-field">
            <span class="search-field__icon">${icon("search", { size: 20 })}</span>
            <input
                id="member-search"
                type="search"
                placeholder="Поиск по user id или имени"
                value="${escapeHtml(state.memberSearch)}"
                autocomplete="off">
        </div>

        <div class="member-list">
            ${members.length === 0
                ? '<p class="hint">Никого не нашли. Попробуй другой запрос.</p>'
                : members.map(member => `
                    <button
                        class="member-card ${state.creditMember?.telegramUserId === member.telegramUserId ? "selected" : ""}"
                        type="button"
                        data-member-id="${member.telegramUserId}">
                        <div class="member-card__head">
                            <strong>${escapeHtml(member.displayName)}</strong>
                            ${member.isOwner ? '<span class="owner-badge">Owner</span>' : ""}
                        </div>
                        <div class="hint">id: ${member.telegramUserId}</div>
                        <div class="member-card__balances">${renderMemberBalances(bank, member.balances)}</div>
                    </button>`).join("")}
        </div>

        <button class="submit-button credit-next-btn" id="go-credit-add" type="button" ${state.creditMember ? "" : "disabled"}>
            ${icon("add", { size: 18, color: "#fff" })}
            Добавить
        </button>`;

    document.getElementById("back-to-settings").addEventListener("click", () => {
        state.screen = "settings";
        render();
    });

    document.getElementById("member-search").addEventListener("input", event => {
        state.memberSearch = event.target.value;
        renderCreditUsers();
    });

    content.querySelectorAll("[data-member-id]").forEach(button => {
        button.addEventListener("click", () => {
            const memberId = Number(button.dataset.memberId);
            state.creditMember = (bank.members || []).find(item => item.telegramUserId === memberId) || null;
            renderCreditUsers();
        });
    });

    document.getElementById("go-credit-add").addEventListener("click", () => {
        if (!state.creditMember) {
            return;
        }

        state.screen = "credit-add";
        render();
    });
}

function renderCreditAdd() {
    const bank = state.bank;
    const member = state.creditMember;

    if (!bank?.canManage || !member) {
        state.screen = "credit-users";
        render();
        return;
    }

    content.innerHTML = `
        <button class="back-link" id="back-to-credit-users" type="button">← Участники</button>
        <h2 class="section-title">Начисление</h2>

        <div class="panel highlight-card">
            <div>
                <strong>${escapeHtml(member.displayName)}</strong>
                <div class="hint">id: ${member.telegramUserId}</div>
                <div class="member-card__balances">${renderMemberBalances(bank, member.balances)}</div>
            </div>
        </div>

        <form class="form-card panel" id="credit-form">
            <label class="form-label" for="credit-currency">Валюта</label>
            ${renderCurrencySelect(bank, { id: "credit-currency-select" })}

            <label class="form-label" for="credit-amount">Сумма</label>
            <input id="credit-amount" name="amount" type="number" min="0.01" step="0.01" placeholder="Сколько начислить" required>

            <button class="submit-button" type="submit">
                ${icon("add", { size: 18, color: "#fff" })}
                Начислить
            </button>
        </form>`;

    bindCurrencySelects(content);

    document.getElementById("back-to-credit-users").addEventListener("click", () => {
        state.screen = "credit-users";
        render();
    });

    document.getElementById("credit-form").addEventListener("submit", async event => {
        event.preventDefault();
        const data = new FormData(event.target);
        const amount = Number(data.get("amount"));

        if (!Number.isFinite(amount) || amount <= 0) {
            showError(new Error("Укажи сумму больше нуля."));
            return;
        }

        try {
            await apiPost(`/api/banks/${bank.id}/credit`, {
                targetTelegramUserId: member.telegramUserId,
                currencyCode: data.get("currencyCode"),
                amount
            });
            await refreshBank();
            state.creditMember = (state.bank.members || []).find(item => item.telegramUserId === member.telegramUserId) || null;
            state.screen = "credit-users";
            render();
        } catch (error) {
            showError(error);
        }
    });
}

function renderFineUsers() {
    const bank = state.bank;
    if (!bank?.canManage) {
        content.innerHTML = '<p class="hint">Штрафы доступны только создателю банка.</p>';
        return;
    }

    const members = filterMembers(bank.members || [], state.memberSearch);

    content.innerHTML = `
        <button class="back-link" id="back-to-settings" type="button">← Управление</button>
        <h2 class="section-title">Штраф — участник</h2>

        <div class="search-field">
            <span class="search-field__icon">${icon("search", { size: 20 })}</span>
            <input
                id="member-search"
                type="search"
                placeholder="Поиск по user id или имени"
                value="${escapeHtml(state.memberSearch)}"
                autocomplete="off">
        </div>

        <div class="member-list">
            ${members.length === 0
                ? '<p class="hint">Никого не нашли. Попробуй другой запрос.</p>'
                : members.map(member => `
                    <button
                        class="member-card ${state.fineMember?.telegramUserId === member.telegramUserId ? "selected" : ""}"
                        type="button"
                        data-member-id="${member.telegramUserId}">
                        <div class="member-card__head">
                            <strong>${escapeHtml(member.displayName)}</strong>
                            ${member.isOwner ? '<span class="owner-badge">Owner</span>' : ""}
                        </div>
                        <div class="hint">id: ${member.telegramUserId}</div>
                        <div class="member-card__balances">${renderMemberBalances(bank, member.balances)}</div>
                    </button>`).join("")}
        </div>

        <button class="submit-button fine-next-btn" id="go-fine-add" type="button" ${state.fineMember ? "" : "disabled"}>
            ${icon("attention", { size: 18, color: "#fff" })}
            Далее
        </button>`;

    document.getElementById("back-to-settings").addEventListener("click", () => {
        state.screen = "settings";
        render();
    });

    document.getElementById("member-search").addEventListener("input", event => {
        state.memberSearch = event.target.value;
        renderFineUsers();
    });

    content.querySelectorAll("[data-member-id]").forEach(button => {
        button.addEventListener("click", () => {
            const memberId = Number(button.dataset.memberId);
            state.fineMember = (bank.members || []).find(item => item.telegramUserId === memberId) || null;
            renderFineUsers();
        });
    });

    document.getElementById("go-fine-add").addEventListener("click", () => {
        if (!state.fineMember) {
            return;
        }

        state.screen = "fine-add";
        render();
    });
}

function renderFineAdd() {
    const bank = state.bank;
    const member = state.fineMember;

    if (!bank?.canManage || !member) {
        state.screen = "fine-users";
        render();
        return;
    }

    content.innerHTML = `
        <button class="back-link" id="back-to-fine-users" type="button">← Участники</button>
        <h2 class="section-title">Выписка штрафа</h2>

        <div class="panel highlight-card">
            <div>
                <strong>${escapeHtml(member.displayName)}</strong>
                <div class="hint">id: ${member.telegramUserId}</div>
                <div class="member-card__balances">${renderMemberBalances(bank, member.balances)}</div>
            </div>
        </div>

        <form class="form-card panel" id="fine-form">
            <label class="form-label" for="fine-currency">Валюта</label>
            ${renderCurrencySelect(bank, { id: "fine-currency-select" })}

            <label class="form-label" for="fine-amount">Сумма</label>
            <input id="fine-amount" name="amount" type="number" min="0.01" step="0.01" placeholder="Сколько списать" required>

            <label class="form-label" for="fine-reason">Причина</label>
            <textarea id="fine-reason" name="reason" placeholder="За что штраф" maxlength="256" rows="3" required></textarea>

            <button class="submit-button submit-button--danger" type="submit">
                ${icon("attention", { size: 18, color: "#fff" })}
                Выписать штраф
            </button>
        </form>`;

    bindCurrencySelects(content);

    document.getElementById("back-to-fine-users").addEventListener("click", () => {
        state.screen = "fine-users";
        render();
    });

    document.getElementById("fine-form").addEventListener("submit", async event => {
        event.preventDefault();
        const data = new FormData(event.target);
        const amount = Number(data.get("amount"));
        const reason = data.get("reason")?.toString().trim();

        if (!Number.isFinite(amount) || amount <= 0) {
            showError(new Error("Укажи сумму больше нуля."));
            return;
        }

        if (!reason) {
            showError(new Error("Укажи причину штрафа."));
            return;
        }

        try {
            await apiPost(`/api/banks/${bank.id}/fine`, {
                targetTelegramUserId: member.telegramUserId,
                currencyCode: data.get("currencyCode"),
                amount,
                reason
            });
            await refreshBank();
            state.fineMember = (state.bank.members || []).find(item => item.telegramUserId === member.telegramUserId) || null;
            state.screen = "fine-users";
            render();
        } catch (error) {
            showError(error);
        }
    });
}

function renderTransactionItem(tx, bank = state.bank) {
    const txMeta = getCurrencyMeta(bank, tx.currencyCode);
    return `
        <div class="tx-item">
            <div class="tx-left">
                <div class="debt-icon debt-icon--${txMeta.bar}">${renderCurrencyIconDisplay(txMeta.icon, "debt-icon__graphic")}</div>
                <div>
                    <div>${escapeHtml(tx.description)}</div>
                    <div class="hint">${formatDate(tx.occurredAt)}</div>
                </div>
            </div>
            <div class="tx-amount ${tx.amount >= 0 ? "positive" : "negative"}">
                ${tx.amount >= 0 ? "+" : ""}${tx.amount}
            </div>
        </div>`;
}

function formatTransaction(tx) {
    const meta = getCurrencyMeta(state.bank, tx.currencyCode);
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
    content.innerHTML = `
        <div class="error-state">
            ${icon("attention", { bg: true, size: 36, bgColor: "#ffe8e7", color: "var(--coral-dark)" })}
            <p class="error">${escapeHtml(error.message || String(error))}</p>
        </div>`;
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
}

content.addEventListener("click", event => {
    if (event.target.closest(".notifications-trigger")) {
        openNotifications().catch(showError);
        return;
    }

    if (event.target.closest(".app-settings-trigger")) {
        openAppSettings();
    }
});

navBanks.addEventListener("click", () => loadMenu().catch(showError));

navHome.addEventListener("click", () => {
    if (!state.selectedBankId) {
        return;
    }

    if (state.bank?.id === state.selectedBankId) {
        state.screen = "bank";
        render();
        return;
    }

    loadBank(state.selectedBankId).catch(showError);
});

navShop.addEventListener("click", async () => {
    if (!state.selectedBankId) {
        return;
    }

    try {
        if (!(await ensureSelectedBankLoaded())) {
            return;
        }

        state.screen = "shop";
        render();
    } catch (error) {
        showError(error);
    }
});

navCart.addEventListener("click", async () => {
    if (!state.selectedBankId) {
        return;
    }

    try {
        if (!(await ensureSelectedBankLoaded())) {
            return;
        }

        state.screen = "cart";
        render();
    } catch (error) {
        showError(error);
    }
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

async function startApp() {
    if (!initTelegram()) {
        return;
    }

    applyTheme(loadPreferences().themeId);
    await AppIcons.preload();
    mountNavIcons();
    await bootstrap();
}

startApp().catch(showError);
