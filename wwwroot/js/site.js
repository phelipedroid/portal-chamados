// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(() => {
    const config = document.querySelector("[data-auto-refresh]");

    if (!config) {
        return;
    }

    const interval = Number(config.dataset.autoRefresh) || 60000;
    const status = config.querySelector("[data-auto-refresh-status]");
    let requestInProgress = false;

    const setStatus = (message, isError = false) => {
        if (!status) {
            return;
        }

        status.textContent = message ? ` ${message}` : "";
        status.classList.toggle("text-danger", isError);
    };

    const refresh = async () => {
        if (document.hidden || requestInProgress) {
            return;
        }

        requestInProgress = true;

        try {
            const response = await fetch(window.location.href, {
                cache: "no-store",
                credentials: "same-origin",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const html = await response.text();
            const updatedDocument = new DOMParser().parseFromString(
                html,
                "text/html");

            let changed = false;

            document.querySelectorAll("[data-auto-refresh-region]")
                .forEach((currentRegion) => {
                    const updatedRegion = updatedDocument.getElementById(
                        currentRegion.id);

                    if (updatedRegion &&
                        currentRegion.innerHTML !== updatedRegion.innerHTML) {
                        currentRegion.innerHTML = updatedRegion.innerHTML;
                        changed = true;
                    }
                });

            const time = new Intl.DateTimeFormat("pt-BR", {
                hour: "2-digit",
                minute: "2-digit",
                second: "2-digit"
            }).format(new Date());

            setStatus(
                changed
                    ? `Dados atualizados às ${time}.`
                    : `Verificado às ${time}.`);
        } catch {
            setStatus(
                "Não foi possível atualizar agora; nova tentativa em 1 minuto.",
                true);
        } finally {
            requestInProgress = false;
        }
    };

    window.setInterval(refresh, interval);

    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) {
            refresh();
        }
    });
})();

(() => {
    const password = document.getElementById("Senha");
    const toggle = document.getElementById("alternarSenha");

    if (!password || !toggle) {
        return;
    }

    toggle.addEventListener("click", () => {
        const isHidden = password.type === "password";

        password.type = isHidden ? "text" : "password";

        const label = isHidden ? "Ocultar senha" : "Mostrar senha";
        toggle.setAttribute("aria-label", label);
        toggle.setAttribute("title", label);
    });
})();

(() => {
    const profile = document.getElementById("Perfil");
    const managedUnits = document.getElementById("unidades-gerente");

    if (!profile || !managedUnits) {
        return;
    }

    const update = () => {
        managedUnits.classList.toggle(
            "d-none",
            profile.value !== "Gerente");
    };

    profile.addEventListener("change", update);
    update();
})();

(() => {
    document.querySelectorAll("form[data-confirm]")
        .forEach((form) => {
            form.addEventListener("submit", (event) => {
                if (!window.confirm(form.dataset.confirm)) {
                    event.preventDefault();
                }
            });
        });
})();

(() => {
    const modal = document.getElementById("modal-imagem-anexo");
    const image = document.getElementById("imagem-anexo-ampliada");
    const title = document.getElementById("titulo-modal-imagem-anexo");

    if (!modal || !image || !title) {
        return;
    }

    modal.addEventListener("show.bs.modal", (event) => {
        const button = event.relatedTarget;

        if (!(button instanceof HTMLElement)) {
            return;
        }

        image.src = button.dataset.anexoUrl || "";
        image.alt = button.dataset.anexoNome || "Imagem anexada";
        title.textContent = button.dataset.anexoNome || "Visualizar imagem";
    });

    modal.addEventListener("hidden.bs.modal", () => {
        image.removeAttribute("src");
    });
})();

(() => {
    const badge = document.getElementById("contador-notificacoes");
    const list = document.getElementById("lista-notificacoes");

    if (!badge || !list) {
        return;
    }

    let requestInProgress = false;

    const showEmpty = () => {
        const message = document.createElement("div");
        message.className = "px-3 py-4 text-center text-muted small";
        message.textContent = "Você não possui novas notificações.";
        list.replaceChildren(message);
    };

    const createItem = (item) => {
        const link = document.createElement("a");
        link.href = item.url;
        link.className = "list-group-item list-group-item-action px-3 py-3";

        const title = document.createElement("div");
        title.className = "fw-semibold text-truncate";
        title.textContent = `#${item.numero} — ${item.titulo}`;

        const action = document.createElement("div");
        action.className = "small text-dark";
        action.textContent = item.acao;

        const time = document.createElement("div");
        time.className = "small text-muted mt-1";
        time.textContent = new Date(item.criadaEm).toLocaleString(
            "pt-BR",
            {
                dateStyle: "short",
                timeStyle: "short"
            });

        link.append(title, action, time);
        return link;
    };

    const refreshNotifications = async () => {
        if (document.hidden || requestInProgress) {
            return;
        }

        requestInProgress = true;

        try {
            const response = await fetch("/Notificacoes/Pendentes", {
                cache: "no-store",
                credentials: "same-origin"
            });

            if (!response.ok) {
                return;
            }

            const data = await response.json();
            const total = Number(data.total) || 0;

            badge.textContent = total > 99 ? "99+" : String(total);
            badge.classList.toggle("d-none", total === 0);

            if (!Array.isArray(data.itens) || data.itens.length === 0) {
                showEmpty();
                return;
            }

            list.replaceChildren(...data.itens.map(createItem));
        } catch {
            // Mantém o último estado e tenta novamente no próximo intervalo.
        } finally {
            requestInProgress = false;
        }
    };

    refreshNotifications();
    window.setInterval(refreshNotifications, 60000);

    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) {
            refreshNotifications();
        }
    });
})();
