var socket = new WebSocket("ws://127.0.0.1:8010");

socket.onopen = function (e) {
    GenerateKey();
    document.getElementById("logger").textContent += '\nConnected to server!';
}

socket.onclose = function (e) {
    document.getElementById("logger").textContent += '\nDisconnected from the server!';
}

socket.onmessage = function (e) {
	Log('[Chat] Recieved Packet', 'lightgreen', e.data);
    ParsePacket(e.data);
}

socket.onerror = function (e) {
    document.getElementById("logger").textContent += '\nAn error has occured!\n' + e.message;
}

function SendToServer(text) {
    
}

var PacketTypes = {

    Text: function(json) {
        document.getElementById("logger").textContent += '\nPacket From Server: ' + JSON.parse(json);
    },

    PublicKeyExchange: async function(json) {
        RSAKey = await FromRSAPem(json.Key);
    
        Crypto.encrypt({
                name: "RSA-OAEP"
            },
            RSAKey,
            AesKeyBuffer
        ).then((encrypted) => {
            socket.send(JSON.stringify(GeneratePacket(ByteArrayToBase64(encrypted), 'AesKeyExchange', false)));
        });
    }

}
