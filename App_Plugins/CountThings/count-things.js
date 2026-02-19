import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";

const CSV_ROW_MAP = [
    ["Content", "Published", "content-published-count"],
    ["Content", "Unpublished", "content-unpublished-count"],
    ["Content", "Trashed", "content-trashed-count"],
    ["Content", "Redirects", "content-redirects-count"],
    ["Media", "Folders", "media-folder-count"],
    ["Media", "Images", "media-image-count"],
    ["Media", "Videos", "media-videos-count"],
    ["Media", "Audios", "media-audios-count"],
    ["Media", "Other Files", "media-other-files-count"],
    ["Media", "Large Files (>2MB)", "media-large-files-count"],
    ["Users", "Active Users", "users-active-count"],
    ["Users", "Locked Users", "users-locked-count"],
    ["Users", "User Groups", "users-groups-count"],
    ["Users", "Members", "users-members-count"],
    ["Users", "Member Groups", "users-member-groups-count"],
    ["Schema", "Document Types", "schema-doctypes-count"],
    ["Schema", "Templates", "schema-templates-count"],
    ["Schema", "Partials", "schema-partials-count"],
    ["Schema", "Scripts", "schema-scripts-count"],
    ["Schema", "Stylesheets", "schema-stylesheets-count"],
    ["Schema", "Media Types", "schema-mediatypes-count"],
    ["Schema", "Member Types", "schema-membertypes-count"],
    ["Schema", "Data Types", "schema-datatypes-count"],
    ["Schema", "Languages", "schema-languages-count"],
    ["Umbraco Forms", "Forms", "forms-count"],
    ["Umbraco Forms", "Form Entries", "forms-entries-count"],
];

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
                origin: { y: 0.6 }
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
        this.shadowRoot.getElementById("clearStorage").addEventListener("click", () => this.clearStorage()); 
        this.shadowRoot.getElementById("countForms").addEventListener("click", () => this.fetchCount("forms"));
        this.shadowRoot.getElementById("exportCsv").addEventListener("click", () => this.exportCsv());

        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (instance) => {
            this.#notificationContext = instance;
        });
    }

    clearStorage() {
        localStorage.clear();
        this.loadStoredCounts();

        this.#notificationContext?.peek("positive", {
            data: { headline: "Local Storage Cleared!", message: "All stored counts have been reset." },
        });
    }

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
            "media-audios-count",
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
                audios: "media-audios-count",
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
            Object.values(idMap[type]).forEach((elementId) => {
                const el = this.shadowRoot.getElementById(elementId);
                if (el) {
                    el.innerHTML = `<span class="loading-spinner"></span>`;
                    el.closest("uui-badge")?.setAttribute("color", "danger");
                }
            });

            const response = await fetch(`/umbraco/api/countthings/${type}`);
            const data = await response.json();

            if (type === "forms" && data && data.installed === false) {
                ["forms-count", "forms-entries-count"].forEach((id) => {
                    this.setBadge(id, "Not installed", "warning");
                    localStorage.setItem(id, "Not installed");
                });

                this.#notificationContext?.peek("warning", {
                    data: { headline: "Umbraco Forms not installed", message: "Forms counts are unavailable." },
                });
                return; 
            }

            Object.entries(data).forEach(([key, value]) => {
                const elementId = idMap[type][key];
                if (!elementId) return;

                localStorage.setItem(elementId, value);

                if (!value || value === "0") {
                    this.setBadge(elementId, String(value ?? "0"), "danger");
                } else {
                    this.setBadge(elementId, String(value), null);
                }
            });

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



    updateFileTypeCounts(fileTypes) {
        const fileTypesContainer = this.shadowRoot.getElementById("media-filetypes-container");
        fileTypesContainer.innerHTML = ""; 

        Object.entries(fileTypes).forEach(([fileType, count]) => {
            const card = document.createElement("uui-card");
            card.classList.add("subcount-card");
            card.innerHTML = `${fileType.toUpperCase()} <uui-badge>${count}</uui-badge>`;
            fileTypesContainer.appendChild(card);
            
            localStorage.setItem(`media-filetype-${fileType}`, count);
        });
    }


    #getExportFilename() {
        const d = new Date();
        const yyyy = d.getFullYear();
        const mm = String(d.getMonth() + 1).padStart(2, "0");
        const dd = String(d.getDate()).padStart(2, "0");
        return `count-things-export-${yyyy}-${mm}-${dd}.csv`;
    }

    exportCsv() {
        const rows = [["Category", "Metric", "Count"]];
        let hasData = false;

        for (const [category, metric, key] of CSV_ROW_MAP) {
            const value = localStorage.getItem(key);
            if (value && value !== "Not counted") hasData = true;
            rows.push([category, metric, value || "Not counted"]);
        }

        Object.keys(localStorage).sort().forEach((key) => {
            if (key.startsWith("media-filetype-")) {
                hasData = true;
                const ext = key.replace("media-filetype-", "").toUpperCase();
                rows.push(["Media (File Type)", ext, localStorage.getItem(key)]);
            }
        });

        if (!hasData) {
            this.#notificationContext?.peek("warning", {
                data: { headline: "Nothing to export", message: "Run some counts first before exporting." },
            });
            return;
        }

        const csv = rows.map((r) => r.join(",")).join("\r\n") + "\r\n";
        const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = this.#getExportFilename();
        a.style.display = "none";
        a.addEventListener("click", (e) => e.stopPropagation());
        document.body.appendChild(a);
        a.click();
        setTimeout(() => {
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        }, 150);

        this.#notificationContext?.peek("positive", {
            data: { headline: "CSV exported!", message: "Your counts have been downloaded." },
        });
    }

    async fetchAllCounts() {
        const types = ["content", "media", "users", "schema", "forms"];

        const originalNotificationContext = this.#notificationContext;
        this.#notificationContext = null;

        let formsInstalled = true;

        for (const type of types) {
            try {
                const result = await this.fetchCount(type);

                if (type === "forms" && result && result.installed === false) {
                    formsInstalled = false;
                }
            } catch (err) {
                console.error(`Error fetching ${type} in fetchAllCounts`, err);
            }
        }

        this.#notificationContext = originalNotificationContext;

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
