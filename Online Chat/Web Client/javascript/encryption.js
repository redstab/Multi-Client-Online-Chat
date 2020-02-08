var Crypto = window.crypto.subtle;

var AesKey;
var RSAKey;

function ToStringUTF(buf) {
    return new TextDecoder().decode(buf);
}

function ToBase64(raw) {
    return btoa(raw);
}

function FromBase64(encoded) {
    return atob(encoded);
}

function ToByteArray(buffer) {
    return new Uint8Array(buffer);
}

function ToString(buf) {
    return String.fromCharCode.apply(null, new Uint8Array(buf));
}

function ToArrayBuffer(str) {
    var buf = new ArrayBuffer(str.length);
    var bufView = new Uint8Array(buf);
    for (var i = 0, strLen = str.length; i < strLen; i++) {
        bufView[i] = str.charCodeAt(i);
    }
    return buf;
}

function ToDer(pem) {
    return ToByteArray(
        ToArrayBuffer(
            FromBase64(pem)
        )
    );
}

function FromRSA(pem) {
    return Crypto.importKey(
        "spki",
        ToDer(pem), {
            name: "RSA-OAEP",
            hash: "SHA-1"
        },
        false,
        ['encrypt']
    );
}

function ExportCrypto(key) {
    return Crypto.exportKey("raw", key);
}

function GenerateAES() {
    return Crypto.generateKey({
            name: "AES-CBC",
            length: 256
        },
        true,
        [
            'encrypt',
            'decrypt'
        ]
    );
}

async function Encrypt(str, key) {
    var iv = Crypto.getRandomValues(new Uint8Array(16));
    return {
        IV: iv,
        Encrypted: await Crypto.encrypt({
                name: "AES-CBC",
                iv
            },
            key,
            ToArrayBuffer(str)
        )
    };
}

function Decrypt(buffer, key, iv) {
    return Crypto.decrypt({
            name: "AES-CBC",
            iv
        },
        key,
        buffer
    );
}