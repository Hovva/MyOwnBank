const AppIcons = (() => {
    const basePath = "assets/icons/svg";

    const names = [
        "cart",
        "shop",
        "profile",
        "crown",
        "add",
        "delete",
        "notification",
        "settings",
        "attention",
        "empty-cart",
        "search",
        "copy"
    ];

    const cache = new Map();

    async function load(name) {
        if (cache.has(name)) {
            return cache.get(name);
        }

        const response = await fetch(`${basePath}/${name}.svg`);
        if (!response.ok) {
            throw new Error(`Icon '${name}' was not found.`);
        }

        const svg = await response.text();
        cache.set(name, svg);
        return svg;
    }

    function render(name, options = {}) {
        const {
            className = "",
            bg = false,
            color = null,
            bgColor = null,
            size = 24,
            label = ""
        } = options;

        const svg = cache.get(name);
        if (!svg) {
            return `<span class="icon icon--loading ${className}" style="width:${size}px;height:${size}px" aria-hidden="true"></span>`;
        }

        const styles = [`--icon-size:${size}px`];
        if (color) {
            styles.push(`--icon-color:${color}`);
        }

        if (bgColor) {
            styles.push(`--icon-bg:${bgColor}`);
        }

        const classes = ["icon", className, bg ? "icon--bg" : ""].filter(Boolean).join(" ");
        const aria = label ? ` role="img" aria-label="${label}"` : ` aria-hidden="true"`;

        return `<span class="${classes}" style="${styles.join(";")}"${aria}>${svg}</span>`;
    }

    async function preload() {
        await Promise.all(names.map(load));
    }

    function mount(selector, name, options = {}) {
        const element = typeof selector === "string"
            ? document.querySelector(selector)
            : selector;

        if (!element) {
            return;
        }

        element.innerHTML = render(name, options);
    }

    return { names, load, preload, render, mount };
})();
