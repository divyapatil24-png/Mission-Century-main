import { WebSocketServer } from "ws";

const wss = new WebSocketServer({ port: 3000 });

let unity = null;

wss.on("connection", (ws) => {
  unity = ws;
  console.log("Unity connected");

  ws.on("message", (msg) => {
    console.log("From Unity:", msg.toString());
  });
});

// THIS is what Claude will trigger
export function sendToUnity(command) {
  if (unity) {
    console.log("Sending:", command);
    unity.send(command);
  }
}