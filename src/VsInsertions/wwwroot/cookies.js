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
    document.cookie = name + "=" + value + ";secure;samesite=strict";
}

// Insertions page
function getAccessTokenCookie() { return getCookie("access_token"); }
function setAccessTokenCookie(value) { setCookie("access_token", value); }

// Flows page
function getFlowsAdoTokenCookie() { return getCookie("flows_ado_token"); }
function setFlowsAdoTokenCookie(value) { setCookie("flows_ado_token", value); }
