import { createServer } from "node:http";
import { mkdir, readFile, rename, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";

const port = Number.parseInt(process.env.PORT ?? "8088", 10);
const dataRoot = process.env.METADATA_ROOT ?? "/data";
const electionIdPattern = /^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$/;

function sendJson(response, statusCode, body, headers = {}) {
    response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8", ...headers });
    response.end(JSON.stringify(body));
}

function routeFile(pathname) {
    const match = pathname.match(/^\/(elections|digests\/metadata|digests\/audit)\/([^/]+)\.json$/);
    if (!match || !electionIdPattern.test(match[2])) {
        return null;
    }

    return join(dataRoot, match[1], `${match[2]}.json`);
}

async function readRequestJson(request) {
    const chunks = [];
    let length = 0;

    for await (const chunk of request) {
        length += chunk.length;
        if (length > 1024 * 1024) {
            throw new Error("Request body exceeds 1 MiB.");
        }
        chunks.push(chunk);
    }

    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

async function writeJsonAtomic(path, value) {
    await mkdir(dirname(path), { recursive: true });
    const temporaryPath = `${path}.${process.pid}.tmp`;
    await writeFile(temporaryPath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
    await rename(temporaryPath, path);
}

async function publishMetadata(request, response) {
    const payload = await readRequestJson(request);
    const electionId = payload?.electionId;

    if (!electionIdPattern.test(electionId ?? "")) {
        sendJson(response, 400, { error: "A valid electionId is required." });
        return;
    }

    const metadata = typeof payload.metadata === "string"
        ? JSON.parse(payload.metadata)
        : payload.metadata;

    if (metadata?.election?.id !== electionId || typeof metadata?.verification?.metadataHash !== "string") {
        sendJson(response, 400, { error: "Metadata election ID and verification hash are required." });
        return;
    }

    await writeJsonAtomic(join(dataRoot, "elections", `${electionId}.json`), metadata);
    await writeJsonAtomic(
        join(dataRoot, "digests", "metadata", `${electionId}.json`),
        { digest: metadata.verification.metadataHash });

    const location = `/elections/${electionId}.json`;
    sendJson(response, 201, { electionId, location }, { Location: location });
}

const server = createServer(async (request, response) => {
    try {
        const url = new URL(request.url ?? "/", `http://${request.headers.host ?? "localhost"}`);

        if (request.method === "GET" && url.pathname === "/health") {
            sendJson(response, 200, { status: "ok" });
            return;
        }

        if (request.method === "POST" && url.pathname === "/metadata") {
            await publishMetadata(request, response);
            return;
        }

        if (request.method === "GET") {
            const path = routeFile(url.pathname);
            if (path) {
                try {
                    const content = await readFile(path);
                    response.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
                    response.end(content);
                } catch (error) {
                    if (error.code !== "ENOENT") {
                        throw error;
                    }
                    sendJson(response, 404, { error: "Not found." });
                }
                return;
            }
        }

        sendJson(response, 404, { error: "Not found." });
    } catch (error) {
        const statusCode = error instanceof SyntaxError ? 400 : 500;
        sendJson(response, statusCode, { error: statusCode === 400 ? "Invalid JSON." : "Metadata service failure." });
        console.error(error);
    }
});

server.listen(port, "0.0.0.0", () => {
    console.log(`Chain Ballot metadata service listening on port ${port}`);
});