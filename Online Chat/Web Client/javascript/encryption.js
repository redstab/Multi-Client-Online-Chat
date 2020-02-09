var Crypto = window.crypto.subtle;

var AesKey;
var RSAKey;
var AesKeyBuffer;

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

function ByteArrayToBase64(buffer) {
    return ToBase64(ToString(buffer));
}

function Base64ToByteArray(buffer) {
    return ToArrayBuffer(FromBase64(buffer));
}

function ToDer(pem) {
    return ToByteArray(
        ToArrayBuffer(
            FromBase64(pem)
        )
    );
}

function FromRSAPem(pem) {
    const pemHeader = "-----BEGIN PUBLIC KEY-----";
    const pemFooter = "-----END PUBLIC KEY-----";
    pem = pem.substring(pemHeader.length, pem.length - pemFooter.length);
    Log('[Encryption] Imported RSAKey', 'lightgreen', pem);
    Log('[Encryption] All communication is now encrypted', 'lightgreen');

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

async function GenerateKey() {
    AesKey = await GenerateAES();
    Log('[Encryption] Generated AesKey', 'lightgreen', AesKey);
    AesKeyBuffer = ToByteArray(await ExportCrypto(AesKey));
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

function GeneratePacket(json, type, encrypt) {
    var iv = 'blank';
    var payload = json;

    if (encrypt) {
        var encryption = Encrypt(JSON.stringify(json), AesKey);
        iv = ByteArrayToBase64(encryption.IV);
        payload = ByteArrayToBase64(encryption.Encrypted);
    }

    return {
        Type: type,
        IV: iv,
        Packet: payload
    }
}

async function ParsePacket(json) {
    var packet = ParseJSON(json);

    if (packet) {

        var IV = packet.IV;
        var Packet = packet.Packet;
        var TypeLambda = window["PacketTypes"][packet.Type];

        if (IV !== 'blank') {
            Packet = ToStringUTF(await Decrypt(Base64ToByteArray(Packet), AesKey, Base64ToByteArray(IV)));
        }

        if (TypeLambda) {
            TypeLambda(Packet);
        } else {
            return false;
        }

        return true;

    } else {
        return false;
    }
}

function ParseJSON(jsonString) {
    try {
        var o = JSON.parse(jsonString);
        if (o && typeof o === "object") {
            return o;
        }
    } catch (e) {
        return false;
    }
}

function Log(str, color, ...params){
    if(params.length !== 0){
        console.log('%c' + str, 'color: ' + color, params);
    }else{
        console.log('%c' + str, 'color: ' + color);
    }
}