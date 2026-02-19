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
            s.onerror = function () { console.warn("Confetti failed to load"); resolve(); }; 
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
        var q = function (sel) { return root.querySelector(sel); };
        var byId = function (id) { return root.querySelector("#" + id); };

        var CSV_ROW_MAP = [
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
            ["Media", "Total Size", "media-total-size-count"],
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
                "media-folder-count", "media-image-count", "media-videos-count", "media-audios-count",  // <-- NEW
                "media-other-files-count", "media-large-files-count", "media-total-size-count",
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

            var dtCont = byId("content-doctypes-container");
            if (dtCont) {
                dtCont.innerHTML = "";
                Object.keys(localStorage).sort().forEach(function (k) {
                    if (k.indexOf("content-doctype-") === 0) {
                        var alias = k.replace("content-doctype-", "");
                        var count = localStorage.getItem(k);
                        var card = document.createElement("uui-card");
                        card.className = "subcount-card";
                        card.innerHTML = alias + " <uui-badge>" + count + "</uui-badge>";
                        dtCont.appendChild(card);
                    }
                });
            }

            var mtCont = byId("media-mediatypes-container");
            if (mtCont) {
                mtCont.innerHTML = "";
                Object.keys(localStorage).sort().forEach(function (k) {
                    if (k.indexOf("media-mediatype-") === 0) {
                        var alias = k.replace("media-mediatype-", "");
                        var count = localStorage.getItem(k);
                        var card = document.createElement("uui-card");
                        card.className = "subcount-card";
                        card.innerHTML = alias + " <uui-badge>" + count + "</uui-badge>";
                        mtCont.appendChild(card);
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

        function updateContentTypeCounts(contentTypes) {
            var cont = byId("content-doctypes-container");
            if (!cont) return;
            cont.innerHTML = "";
            Object.keys(contentTypes).forEach(function (alias) {
                var count = contentTypes[alias];
                var card = document.createElement("uui-card");
                card.className = "subcount-card";
                card.innerHTML = alias + " <uui-badge>" + count + "</uui-badge>";
                cont.appendChild(card);
                localStorage.setItem("content-doctype-" + alias, count);
            });
        }

        function updateMediaTypeCounts(mediaTypes) {
            var cont = byId("media-mediatypes-container");
            if (!cont) return;
            cont.innerHTML = "";
            Object.keys(mediaTypes).forEach(function (alias) {
                var count = mediaTypes[alias];
                var card = document.createElement("uui-card");
                card.className = "subcount-card";
                card.innerHTML = alias + " <uui-badge>" + count + "</uui-badge>";
                cont.appendChild(card);
                localStorage.setItem("media-mediatype-" + alias, count);
            });
        }

        function formatBytes(bytes) {
            if (bytes === 0) return "0 B";
            var units = ["B", "KB", "MB", "GB", "TB"];
            var i = Math.floor(Math.log(bytes) / Math.log(1024));
            var value = bytes / Math.pow(1024, i);
            return value.toFixed(i === 0 ? 0 : 1) + " " + units[i];
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
                    audios: "media-audios-count",
                    other: "media-other-files-count",
                    largeFiles: "media-large-files-count",
                    totalSize: "media-total-size-count"
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

                if (type === "content" && data.contentTypes) updateContentTypeCounts(data.contentTypes);

                if (type === "media") {
                    if (data.totalSize !== undefined) {
                        var formatted = formatBytes(data.totalSize);
                        setBadge("media-total-size-count", formatted, null);
                        localStorage.setItem("media-total-size-count", formatted);
                    }
                    if (data.fileTypes) updateFileTypeCounts(data.fileTypes);
                    if (data.mediaTypes) updateMediaTypeCounts(data.mediaTypes);
                }
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

        function getExportFilename() {
            var d = new Date();
            var yyyy = d.getFullYear();
            var mm = String(d.getMonth() + 1).padStart(2, "0");
            var dd = String(d.getDate()).padStart(2, "0");
            return "count-things-export-" + yyyy + "-" + mm + "-" + dd + ".csv";
        }

        function exportCsv() {
            var rows = [["Category", "Metric", "Count"]];
            var hasData = false;

            CSV_ROW_MAP.forEach(function (entry) {
                var category = entry[0], metric = entry[1], key = entry[2];
                var value = localStorage.getItem(key);
                if (value && value !== "Not counted") hasData = true;
                rows.push([category, metric, value || "Not counted"]);
            });

            Object.keys(localStorage).sort().forEach(function (key) {
                if (key.indexOf("content-doctype-") === 0) {
                    hasData = true;
                    var alias = key.replace("content-doctype-", "");
                    rows.push(["Content (Document Type)", alias, localStorage.getItem(key)]);
                }
            });

            Object.keys(localStorage).sort().forEach(function (key) {
                if (key.indexOf("media-filetype-") === 0) {
                    hasData = true;
                    var ext = key.replace("media-filetype-", "").toUpperCase();
                    rows.push(["Media (File Type)", ext, localStorage.getItem(key)]);
                }
            });

            Object.keys(localStorage).sort().forEach(function (key) {
                if (key.indexOf("media-mediatype-") === 0) {
                    hasData = true;
                    var alias = key.replace("media-mediatype-", "");
                    rows.push(["Media (Media Type)", alias, localStorage.getItem(key)]);
                }
            });

            if (!hasData) {
                alert("Nothing to export. Run some counts first.");
                return;
            }

            var csv = rows.map(function (r) { return r.join(","); }).join("\r\n") + "\r\n";
            var blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
            var url = URL.createObjectURL(blob);
            var a = document.createElement("a");
            a.href = url;
            a.download = getExportFilename();
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        }

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
        byId("exportCsv")?.addEventListener("click", function () { exportCsv(); });
    }

    w.CountThingsV13 = { init: init };
})(window);
