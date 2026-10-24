import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { sendToUnity } from "./server.js";

const server = new Server(
  { name: "unity-controller", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

// Tool list for Claude
server.setRequestHandler("tools/list", async () => {
  return {
    tools: [
      {
        name: "unity_command",
        description: "Send command to Unity",
        inputSchema: {
          type: "object",
          properties: {
            command: { type: "string" }
          }
        }
      }
    ]
  };
});

// Tool execution
server.setRequestHandler("tools/call", async (req) => {
  if (req.params.name === "unity_command") {
    const cmd = req.params.arguments.command;

    sendToUnity(cmd);

    return {
      content: [
        {
          type: "text",
          text: `Sent to Unity: ${cmd}`
        }
      ]
    };
  }
});

const transport = new StdioServerTransport();
await server.connect(transport);