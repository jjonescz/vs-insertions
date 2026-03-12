function getCookie(name) {
    const prefix = name + "=";
    const decodedCookie = decodeURIComponent(document.cookie);
    const cookies = decodedCookie.split(';');
    for (const cookie of cookies) {
        if (cookie.trim().startsWith(prefix)) {
            return cookie.trim().substring(prefix.length);
        }
    }
    return "";
}

function setCookie(name, value) {
    document.cookie = name + "=" + encodeURIComponent(value) + ";path=/;secure;samesite=strict";
}
