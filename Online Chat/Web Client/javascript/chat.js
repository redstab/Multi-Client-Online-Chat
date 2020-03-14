// var socket = new WebSocket("ws://127.0.0.1:8010");

// socket.onopen = function (e) {
//     GenerateKey();
//     document.getElementById("logger").textContent += '\nConnected to server!';
// }

// socket.onclose = function (e) {
//     document.getElementById("logger").textContent += '\nDisconnected from the server!';
// }

// socket.onmessage = function (e) {
// 	Log('[Chat] Recieved Packet', 'lightgreen', e.data);
//     ParsePacket(e.data);
// }

// socket.onerror = function (e) {
//     document.getElementById("logger").textContent += '\nAn error has occured!\n' + e.message;
// }

class Connection{
    socket;
    ready = false;
    constructor(ip, port){
        this.ip = ip;
        this.port = port;
    }

    Connect(){
        return new Promise((resolve, reject) => {
            this.socket = new WebSocket('ws://' + this.ip + ':' + this.port);
    
            this.socket.onopen = (event => {
                Log("[Authentication] Server Connection established", 'lightgreen');
                resolve();
            });
            
            this.socket.onerror = (event => {
                Log("[Error] Websocket encountered error: ", 'red', event);
                reject();
            });
            
            this.socket.onmessage = (event => {
                Log("[Communication] Websocket received a new message: ", 'lightgreen', event.data);
                ParsePacket(event.data);
            });

            this.socket.onclose = (event) => {
                Log("[Error] Websocket disconnected", 'red', event);
            }
            
        });
    }

}

var ServerConnection;

$(document).ready(function(){
    GenerateKey();
});

function WaitEncryption(callback){
    if(ServerConnection.ready == false){
        setTimeout(WaitEncryption, 50, callback);
    }else{
        setTimeout(callback, 100);
    }
}

async function Login(){

    ServerConnection = new Connection('127.0.0.1', 8010);

    let username = $('input[name="Username"]').val();
    let password = $('input[name="Password"]').val();

    if(username && password){
        await ServerConnection.Connect();
        WaitEncryption(
    
            async function(){       
                ServerConnection.socket.send(JSON.stringify(await GeneratePacket({ Username: username, Password: password}, "Authentication", true)));
            }
    
        );
    }
}

var PacketTypes = {

    Text: function(json) {
        console.log(json);
        
    },

    PublicKeyExchange: async function(json) {
        RSAKey = await FromRSAPem(json.Key);
    
        Crypto.encrypt({
                name: "RSA-OAEP"
            },
            RSAKey,
            AesKeyBuffer
        ).then(async (encrypted) => {
            ServerConnection.socket.send(JSON.stringify(await GeneratePacket(ByteArrayToBase64(encrypted), 'AesKeyExchange', false)));
            ServerConnection.ready = true;
        });
    }
}
