function loadScreen(screen) {
    const container = document.getElementById("screenContainer");
    if (!container) return;

    container.innerHTML = "<div style='padding:20px;'>Loading...</div>";

    fetch(`/Index?handler=LoadScreen&screen=${screen}`)
        .then(r => r.text())
        .then(html => {
            // 1️⃣ Replace DOM
            container.innerHTML = html;

            // 2️⃣ Bind events AFTER DOM exists
            if (typeof bindHmiButtons === 'function') {
                bindHmiButtons();
            }

            // 3️⃣ Apply color AFTER buttons exist
            if (typeof updateButtonStyles === 'function') {
                updateButtonStyles();
            }
        })
        .catch(e => {
            container.innerHTML = "Error loading screen";
            console.error(e);
        });
}

