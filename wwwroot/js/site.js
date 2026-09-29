(() => {
    const validationUrl = "/Home/ValidateRequired";

    document.querySelectorAll("form[data-ajax-check]").forEach((form) => {
        form.noValidate = true;
        form.addEventListener("submit", async (event) => {
            if (form.dataset.ajaxValidated === "true") {
                delete form.dataset.ajaxValidated;
                return;
            }

            event.preventDefault();
            const fields = {};
            form.querySelectorAll("input[required], select[required], textarea[required]").forEach((input) => {
                if (input.type === "checkbox" || input.type === "radio") {
                    if (!input.checked) fields[input.name] = "";
                    else fields[input.name] = input.value;
                } else {
                    fields[input.name] = input.value;
                }
            });

            const button = form.querySelector("button[type='submit'], button:not([type])");
            if (button) button.disabled = true;
            try {
                const response = await fetch(validationUrl, {
                    method: "POST",
                    headers: { "Content-Type": "application/json", "X-Requested-With": "XMLHttpRequest" },
                    body: JSON.stringify(fields)
                });
                const result = await response.json();
                form.querySelectorAll(".ajax-field-error").forEach((item) => item.remove());

                if (!result.valid) {
                    Object.entries(result.errors).forEach(([name, message]) => {
                        const field = form.querySelector(`[name="${CSS.escape(name)}"]`);
                        if (!field) return;
                        const error = document.createElement("span");
                        error.className = "field-error ajax-field-error";
                        error.textContent = message;
                        field.closest("label")?.after(error);
                    });
                    form.querySelector("[required]:invalid, input[required]:placeholder-shown")?.focus();
                    return;
                }

                form.dataset.ajaxValidated = "true";
                form.requestSubmit(event.submitter ?? undefined);
            } catch {
                form.reportValidity();
            } finally {
                if (button) button.disabled = false;
            }
        });
    });
})();
