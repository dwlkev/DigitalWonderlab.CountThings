import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";

export default class CountThingsDashboard extends UmbElementMixin(HTMLElement) {
    /** @type {import('@umbraco-cms/backoffice/notification').UmbNotificationContext} */
    #notificationContext;

    constructor() {
        super();
        this.attachShadow({ mode: "open" });

        this.loadTemplate();
        this.loadStyles();
    }

    async loadTemplate() {
        const response = await fetch("/App_Plugins/CountThings/count-things-template.html");
        const templateText = await response.text();
        const template = document.createElement("template");
        template.innerHTML = templateText;

        this.shadowRoot.appendChild(template.content.cloneNode(true));

        this.afterTemplateLoaded();
        this.loadStoredCounts();

        // Load ConfettiJS dynamically if not already included
        if (!window.confetti) {
            const script = document.createElement("script");
            script.src = "https://cdn.jsdelivr.net/npm/canvas-confetti@1.5.1/dist/confetti.browser.min.js";
            script.onload = () => console.log("ConfettiJS Loaded");
            document.head.appendChild(script);
        }

    }

    confettiEffect() {
        if (window.confetti) {
            confetti({
                particleCount: 100,
                spread: 70,
                origin: { y: 0.6 } // Adjust origin to appear more natural
            });
        }
    }


    loadStyles() {
        const style = document.createElement("link");
        style.setAttribute("rel", "stylesheet");
        style.setAttribute("href", "/App_Plugins/CountThings/count-things-style.css");
        this.shadowRoot.appendChild(style);
    }

    afterTemplateLoaded() {
        this.loadStoredCounts();

        this.shadowRoot.getElementById("countContent").addEventListener("click", () => this.fetchCount("content"));
        this.shadowRoot.getElementById("countMedia").addEventListener("click", () => this.fetchCount("media"));
        this.shadowRoot.getElementById("countUsers").addEventListener("click", () => this.fetchCount("users"));
        this.shadowRoot.getElementById("countSchema").addEventListener("click", () => this.fetchCount("schema"));
        this.shadowRoot.getElementById("countAll").addEventListener("click", () => this.fetchAllCounts());
        this.shadowRoot.getElementById("clearStorage").addEventListener("click", () => this.clearStorage()); //  Added Clear Storage
        this.shadowRoot.getElementById("countForms").addEventListener("click", () => this.fetchCount("forms"));

        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (instance) => {
            this.#notificationContext = instance;
        });
    }

    /** Clears local storage and resets UI */
    clearStorage() {
        localStorage.clear();
        this.loadStoredCounts();

        this.#notificationContext?.peek("positive", {
            data: { headline: "Local Storage Cleared!", message: "All stored counts have been reset." },
        });
    }

    /** Set a badge's text and optional color (danger|warning|success|primary|...); pass null to clear color */
    setBadge(id, text, color = null) {
        const el = this.shadowRoot.getElementById(id);
        if (!el) return;
        el.innerText = text;
        const badge = el.closest("uui-badge");
        if (!badge) return;
        if (color) badge.setAttribute("color", color);
        else badge.removeAttribute("color");
    }


    loadStoredCounts() {
        const keys = [
            "content-count",
            "content-published-count",
            "content-unpublished-count",
            "content-trashed-count",
            "content-redirects-count",
            "media-count",
            "media-folder-count",
            "media-image-count",
            "media-videos-count",
            //"media-large-images-count",
            "media-large-files-count",
            "media-other-files-count",
            "users-total-count",
            "users-active-count",
            "users-locked-count",
            "users-groups-count",
            "users-members-count",
            "users-member-groups-count",
            "schema-total-count",
            "schema-doctypes-count",
            "schema-templates-count",
            "schema-partials-count",
            "schema-scripts-count",
            "schema-stylesheets-count",
            "schema-mediatypes-count",
            "schema-membertypes-count",
            "schema-datatypes-count",
            "schema-languages-count",
            "forms-count",
            "forms-entries-count"
        ];

        keys.forEach((key) => {
            const storedValue = localStorage.getItem(key);
            const el = this.shadowRoot.getElementById(key);
            if (!el) return;

            if (!storedValue || storedValue === "Not counted") {
                this.setBadge(key, "Not counted", "danger");
            } else if (storedValue === "Not installed") {
                this.setBadge(key, "Not installed", "warning");
            } else {
                this.setBadge(key, storedValue, null);
            }
        });

        // Restore file type counts dynamically
        const fileTypesContainer = this.shadowRoot.getElementById("media-filetypes-container");
        if (fileTypesContainer) {
            fileTypesContainer.innerHTML = "";
            Object.keys(localStorage).forEach((key) => {
                if (key.startsWith("media-filetype-")) {
                    const fileType = key.replace("media-filetype-", "").toUpperCase();
                    const count = localStorage.getItem(key);
                    const card = document.createElement("uui-card");
                    card.classList.add("subcount-card");
                    card.innerHTML = `${fileType} <uui-badge>${count}</uui-badge>`;
                    fileTypesContainer.appendChild(card);
                }
            });
        }
    }



    /** Fetch and update a specific count with a loading state */
    async fetchCount(type) {
        const idMap = {
            content: {
                total: "content-count",
                published: "content-published-count",
                unpublished: "content-unpublished-count",
                trashed: "content-trashed-count",
                redirects: "content-redirects-count"
            },
            media: {
                total: "media-count",
                folders: "media-folder-count",
                images: "media-image-count",
                videos: "media-videos-count",
                //largeImages: "media-large-images-count",
                largeFiles: "media-large-files-count",
                other: "media-other-files-count"
            },
            users: {
                total: "users-total-count",
                active: "users-active-count",
                locked: "users-locked-count",
                userGroups: "users-groups-count",
                members: "users-members-count",
                memberGroups: "users-member-groups-count"
            },
            schema: {
                total: "schema-total-count",
                doctypes: "schema-doctypes-count",
                templates: "schema-templates-count",
                partials: "schema-partials-count",
                scripts: "schema-scripts-count",
                stylesheets: "schema-stylesheets-count",
                mediatypes: "schema-mediatypes-count",
                membertypes: "schema-membertypes-count",
                datatypes: "schema-datatypes-count",
                languages: "schema-languages-count"
            },
            forms: {
                total: "forms-count",
                entries: "forms-entries-count"
            }
        };

        try {
            // set loading spinners (red while loading)
            Object.values(idMap[type]).forEach((elementId) => {
                const el = this.shadowRoot.getElementById(elementId);
                if (el) {
                    el.innerHTML = `<span class="loading-spinner"></span>`;
                    el.closest("uui-badge")?.setAttribute("color", "danger");
                }
            });

            const response = await fetch(`/umbraco/api/countthings/${type}`);
            const data = await response.json();

            // Special handling for Forms not installed
            if (type === "forms" && data && data.installed === false) {
                ["forms-count", "forms-entries-count"].forEach((id) => {
                    this.setBadge(id, "Not installed", "warning");
                    localStorage.setItem(id, "Not installed");
                });

                this.#notificationContext?.peek("warning", {
                    data: { headline: "Umbraco Forms not installed", message: "Forms counts are unavailable." },
                });
                return; // stop normal mapping for forms
            }

            // Normal mapping
            Object.entries(data).forEach(([key, value]) => {
                const elementId = idMap[type][key];
                if (!elementId) return;

                // persist + set badge color
                localStorage.setItem(elementId, value);

                if (!value || value === "0") {
                    this.setBadge(elementId, String(value ?? "0"), "danger");
                } else {
                    this.setBadge(elementId, String(value), null);
                }
            });

            // Handle media file types dynamically
            if (type === "media" && data.fileTypes) {
                this.updateFileTypeCounts(data.fileTypes);
            }

            this.#notificationContext?.peek("positive", {
                data: { headline: `${type.charAt(0).toUpperCase() + type.slice(1)} count updated!`, message: "Counts are now up to date." },
            });

        } catch (error) {
            console.error(`Failed to fetch ${type} count`, error);
            this.#notificationContext?.peek("danger", {
                data: { headline: `Error updating ${type} count!`, message: "Please check the API connection." },
            });
        }
    }



    /** Dynamically update file type counts */
    updateFileTypeCounts(fileTypes) {
        const fileTypesContainer = this.shadowRoot.getElementById("media-filetypes-container");
        fileTypesContainer.innerHTML = ""; // Clear previous file type entries

        Object.entries(fileTypes).forEach(([fileType, count]) => {
            const card = document.createElement("uui-card");
            card.classList.add("subcount-card");
            card.innerHTML = `${fileType.toUpperCase()} <uui-badge>${count}</uui-badge>`;
            fileTypesContainer.appendChild(card);

            // Store in local storage
            localStorage.setItem(`media-filetype-${fileType}`, count);
        });
    }


    /** Fetch and update all counts, but only show one notification at the end */
    async fetchAllCounts() {
        const types = ["content", "media", "users", "schema", "forms"];

        // Disable notifications temporarily
        const originalNotificationContext = this.#notificationContext;
        this.#notificationContext = null;

        let formsInstalled = true;

        // Fetch all counts
        for (const type of types) {
            try {
                const result = await this.fetchCount(type);

                // fetchCount already handles setting badges, but we can detect the forms case here too
                if (type === "forms" && result && result.installed === false) {
                    formsInstalled = false;
                }
            } catch (err) {
                console.error(`Error fetching ${type} in fetchAllCounts`, err);
            }
        }

        // Restore notification context
        this.#notificationContext = originalNotificationContext;

        // Show only one notification at the end
        if (formsInstalled) {
            this.#notificationContext?.peek("positive", {
                data: {
                    headline: "All counts updated!",
                    message: "Content, Media, Users, Schema, and Forms counts are refreshed."
                },
            });
        } else {
            this.#notificationContext?.peek("positive", {
                data: {
                    headline: "All counts updated",
                    message: "Content, Media, Users, and Schema counts are refreshed."
                },
            });
        }

        this.confettiEffect();
    }


}

customElements.define("count-things-dashboard", CountThingsDashboard);
