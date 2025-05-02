function toggleCustomInput() {
    const select = document.getElementById('language');
    const customDiv = document.getElementById('customLanguageDiv');
    const customInput = document.getElementById('customLanguage');

    if (select.value === 'custom') {
        customDiv.classList.remove('d-none');
        customInput.required = true;
    } else {
        customDiv.classList.add('d-none');
        customInput.required = false;
    }
}
function togglePosterInput() {
    const selected = document.querySelector('input[name="posterOption"]:checked').value;
    document.getElementById('uploadDiv').style.display = selected === 'upload' ? 'block' : 'none';
    document.getElementById('linkDiv').style.display = selected === 'link' ? 'block' : 'none';
}

function previewPoster(event) {
    const file = event.target.files[0];
    if (file) {
        const reader = new FileReader();
        reader.onload = function (e) {
            const img = document.getElementById('posterPreview');
            img.src = e.target.result;
            img.style.display = 'block';
        };
        reader.readAsDataURL(file);
    }
}

function previewLink(event) {
    const url = event.target.value;
    const img = document.getElementById('posterPreview');
    if (url.trim()) {
        img.src = url;
        img.style.display = 'block';           // ← show it
    } 
    else {
        img.style.display = 'none';
    }
}

function toggleCustomLanguage() {
    var select = document.getElementById("language");
    var customInput = document.getElementById("customLanguage");
    customInput.style.display = select.value === "custom" ? "block" : "none";
}

