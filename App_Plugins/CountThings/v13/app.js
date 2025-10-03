(function (w) {

    var _ctConfettiReady = null;

    function ensureConfetti() {
        if (window.confetti) return Promise.resolve();
        if (_ctConfettiReady) return _ctConfettiReady;

        _ctConfettiReady = new Promise(function (resolve, reject) {
            var s = document.createElement("script");
            s.src = "https://cdn.jsdelivr.net/npm/canvas-confetti@1.6.0/dist/confetti.browser.min.js";
            s.async = true;
            s.onload = function () { resolve(); };
            s.onerror = function () { console.warn("Confetti failed to load"); resolve(); }; // soft-fail
            document.head.appendChild(s);
        });

        return _ctConfettiReady;
    }

    function fireConfetti() {
        if (!window.confetti) return;
        window.confetti({
            particleCount: 120,
            spread: 70,
            origin: { y: 0.6 }
        });
    }

    function init(root) {
        // helpers scoped to the dashboard root
        var q = function (sel) { return root.querySelector(sel); };
        var byId = function (id) { return root.querySelector("#" + id); };

        function setBadge(id, text, color) {
            var el = byId(id);
            if (!el) return;
            el.textContent = text;
            var badge = el.closest("uui-badge");
            if (!badge) return;
            if (color) badge.setAttribute("color", color);
            else badge.removeAttribute("color");
        }

        function spinner(badgeId) {
            var el = byId(badgeId);
            if (el) {
                el.innerHTML = '<span class="loading-spinner"></span>';
                var b = el.closest("uui-badge");
                if (b) b.setAttribute("color", "danger");
            }
        }

        function loadStoredCounts() {
            var keys = [
                "content-published-count", "content-unpublished-count", "content-trashed-count", "content-redirects-count",
                "media-folder-count", "media-image-count", "media-videos-count", "media-other-files-count", "media-large-files-count",
                "users-active-count", "users-locked-count", "users-groups-count", "users-members-count", "users-member-groups-count",
                "schema-doctypes-count", "schema-templates-count", "schema-partials-count", "schema-scripts-count",
                "schema-stylesheets-count", "schema-mediatypes-count", "schema-membertypes-count", "schema-datatypes-count",
                "schema-languages-count", "forms-count", "forms-entries-count"
            ];

            keys.forEach(function (k) {
                var v = localStorage.getItem(k);
                if (!v) setBadge(k, "Not counted", "danger");
                else if (v === "Not installed") setBadge(k, v, "warning");
                else setBadge(k, v, null);
            });

            var cont = byId("media-filetypes-container");
            if (cont) {
                cont.innerHTML = "";
                Object.keys(localStorage).forEach(function (k) {
                    if (k.indexOf("media-filetype-") === 0) {
                        var ext = k.replace("media-filetype-", "").toUpperCase();
                        var count = localStorage.getItem(k);
                        var card = document.createElement("uui-card");
                        card.className = "subcount-card";
                        card.innerHTML = ext + " <uui-badge>" + count + "</uui-badge>";
                        cont.appendChild(card);
                    }
                });
            }
        }

        function updateFileTypeCounts(fileTypes) {
            var cont = byId("media-filetypes-container");
            if (!cont) return;
            cont.innerHTML = "";
            Object.keys(fileTypes).forEach(function (ext) {
                var count = fileTypes[ext];
                var card = document.createElement("uui-card");
                card.className = "subcount-card";
                card.innerHTML = ext.toUpperCase() + " <uui-badge>" + count + "</uui-badge>";
                cont.appendChild(card);
                localStorage.setItem("media-filetype-" + ext, count);
            });
        }

        async function fetchCount(type) {
            var idMap = {
                content: {
                    published: "content-published-count",
                    unpublished: "content-unpublished-count",
                    trashed: "content-trashed-count",
                    redirects: "content-redirects-count"
                },
                media: {
                    folders: "media-folder-count",
                    images: "media-image-count",
                    videos: "media-videos-count",
                    other: "media-other-files-count",
                    largeFiles: "media-large-files-count"
                },
                users: {
                    active: "users-active-count",
                    locked: "users-locked-count",
                    userGroups: "users-groups-count",
                    members: "users-members-count",
                    memberGroups: "users-member-groups-count"
                },
                schema: {
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

            Object.values(idMap[type]).forEach(spinner);

            try {
                var res = await fetch("/umbraco/api/countthings/" + type, { headers: { "x-requested-with": "XMLHttpRequest" } });
                var data = await res.json();

                if (type === "forms" && data && data.installed === false) {
                    ["forms-count", "forms-entries-count"].forEach(function (id) {
                        setBadge(id, "Not installed", "warning");
                        localStorage.setItem(id, "Not installed");
                    });
                    return;
                }

                Object.keys(data).forEach(function (key) {
                    var elId = idMap[type][key];
                    if (!elId) return;
                    var val = data[key];
                    localStorage.setItem(elId, val);
                    if (!val || val === 0 || val === "0") setBadge(elId, String(val ?? "0"), "danger");
                    else setBadge(elId, String(val), null);
                });

                if (type === "media" && data.fileTypes) updateFileTypeCounts(data.fileTypes);
            } catch (e) {
                console.error("fetchCount failed for", type, e);
            }
        }

        async function fetchAll() {
            var types = ["content", "media", "users", "schema", "forms"];
            for (var i = 0; i < types.length; i++) {
                await fetchCount(types[i]);
            }            
            await ensureConfetti();
            fireConfetti();
        }

        // initial state + wiring
        loadStoredCounts();
        byId("countAll")?.addEventListener("click", fetchAll);
        byId("clearStorage")?.addEventListener("click", function () {
            localStorage.clear();
            loadStoredCounts();
        });
        byId("countContent")?.addEventListener("click", function () { fetchCount("content"); });
        byId("countMedia")?.addEventListener("click", function () { fetchCount("media"); });
        byId("countUsers")?.addEventListener("click", function () { fetchCount("users"); });
        byId("countSchema")?.addEventListener("click", function () { fetchCount("schema"); });
        byId("countForms")?.addEventListener("click", function () { fetchCount("forms"); });
    }

    // export
    w.CountThingsV13 = { init: init };
})(window);
