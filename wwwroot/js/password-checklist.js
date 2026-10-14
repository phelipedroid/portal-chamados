document.querySelectorAll("[data-password-checklist]")
    .forEach(function (checklist) {
        const senha = document.getElementById(
            checklist.dataset.passwordInput);

        const confirmacao = document.getElementById(
            checklist.dataset.confirmationInput);

        if (!senha || !confirmacao) {
            return;
        }

        const regras = {
            length: function (valor) {
                return valor.length >= 8;
            },
            uppercase: function (valor) {
                return /[A-Z]/.test(valor);
            },
            lowercase: function (valor) {
                return /[a-z]/.test(valor);
            },
            number: function (valor) {
                return /\d/.test(valor);
            },
            special: function (valor) {
                return /[^a-zA-Z\d]/.test(valor);
            },
            unique: function (valor) {
                return new Set(valor).size >= 4;
            },
            confirmation: function (valor) {
                return valor.length > 0 &&
                    valor === confirmacao.value;
            }
        };

        function atualizar() {
            Object.entries(regras).forEach(
                function ([nome, validar]) {
                    const item = checklist.querySelector(
                        `[data-password-rule="${nome}"]`);

                    if (!item) {
                        return;
                    }

                    const valido = validar(senha.value);
                    const icone = item.querySelector("span");

                    item.classList.toggle(
                        "text-success",
                        valido);

                    item.classList.toggle(
                        "text-muted",
                        !valido);

                    if (icone) {
                        icone.textContent = valido ? "✓" : "○";
                    }
                });
        }

        senha.addEventListener("input", atualizar);
        confirmacao.addEventListener("input", atualizar);
        atualizar();
    });
