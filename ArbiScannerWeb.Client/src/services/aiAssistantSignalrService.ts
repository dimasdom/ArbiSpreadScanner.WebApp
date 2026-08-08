import {
    HubConnection,
    HubConnectionBuilder,
    LogLevel,
} from '@microsoft/signalr';
import { logger } from './loggerService';
import { getAccessToken } from './oidcUserManager';

export interface ChatRequest {
    prompt: string;
    spreadContextId?: string | null;
}

export type ChatStreamChunkKind =
    | 'ConversationStart'
    | 'AnswerDelta'
    | 'SpreadsData'
    | 'SpreadData'
    | 'RecommendedSpreads'
    | 'ToolResult'
    | 'Error'
    | 'ConversationEnd';

interface ChatStreamChunk {
    kind: ChatStreamChunkKind;
    textDelta?: string | null;
    toolName?: string | null;
    data?: unknown;
    message?: string | null;
}

export interface ChatStreamHandlers {
    /** The first event of every stream - the server has started working on the request. */
    onStart: () => void;
    /** A piece of the model's free-text answer, in arrival order. */
    onDelta: (text: string) => void;
    /** get_spreads_for_current_user's result. Just rendered as text for now. */
    onSpreadsData: (data: unknown) => void;
    /** get_spread_by_id's result. Just rendered as text for now. */
    onSpreadData: (data: unknown) => void;
    /** get_recommended_spreads's result - the top 2 ranked spreads per category. */
    onRecommendedSpreads: (data: unknown) => void;
    /** A result from any other MCP tool - fallback for tools without a dedicated event yet. */
    onToolResult: (toolName: string, data: unknown) => void;
    /** A failure chunk from the server, or a transport-level stream failure. */
    onError: (message: string) => void;
    /** The last event of every stream, regardless of how it ended. */
    onFinish: () => void;
}

const AI_HUB_URL = '/ai-hub/chat';

// A separate connection from signalrService's TradeOpportunityHub client — this
// hub lives in a different backend container (ArbiScanner.AiAssistant) with its
// own connection lifecycle, so it isn't a good fit for that service's
// group-join-oriented API.
class AiAssistantSignalrService {
    private connection: HubConnection | null = null;
    private connectingPromise: Promise<void> | null = null;

    private async ensureConnected(): Promise<void> {
        if (this.connection?.state === 'Connected') return;
        if (this.connectingPromise) {
            await this.connectingPromise;
            return;
        }

        // Browsers can't set an Authorization header on a WebSocket handshake —
        // accessTokenFactory instead appends the token as an access_token query
        // param, which the hub's JWT bearer handler only honors for /ai-hub paths.
        const connection = new HubConnectionBuilder()
            .withUrl(AI_HUB_URL, {
                accessTokenFactory: async () => (await getAccessToken()) ?? '',
            })
            .configureLogging(LogLevel.Information)
            .withAutomaticReconnect()
            .build();

        connection.onclose(() => {
            logger.warn('AI assistant hub disconnected', 'aiAssistantSignalrService');
        });

        this.connection = connection;
        this.connectingPromise = connection
            .start()
            .then(() => {
                logger.info('AI assistant hub connected', 'aiAssistantSignalrService');
            })
            .finally(() => {
                this.connectingPromise = null;
            });

        await this.connectingPromise;
    }

    /**
     * Streams one chat turn via StreamMessage, a SignalR streaming hub method (not a plain
     * invoke) — the model's tool-call decision arrives as a single ToolResult chunk, but its
     * free-text answers arrive as a sequence of AnswerDelta chunks as they're generated.
     * Resolves once the stream ends, whether by graceful completion or a transport error.
     */
    async streamMessage(request: ChatRequest, handlers: ChatStreamHandlers): Promise<void> {
        await this.ensureConnected();
        if (!this.connection) throw new Error('No AI assistant connection');
        const connection = this.connection;

        return new Promise<void>((resolve) => {
            connection.stream<ChatStreamChunk>('StreamMessage', request).subscribe({
                next: (chunk) => {
                    switch (chunk.kind) {
                        case 'ConversationStart':
                            handlers.onStart();
                            break;
                        case 'AnswerDelta':
                            if (chunk.textDelta) handlers.onDelta(chunk.textDelta);
                            break;
                        case 'SpreadsData':
                            handlers.onSpreadsData(chunk.data);
                            break;
                        case 'SpreadData':
                            handlers.onSpreadData(chunk.data);
                            break;
                        case 'RecommendedSpreads':
                            handlers.onRecommendedSpreads(chunk.data);
                            break;
                        case 'ToolResult':
                            handlers.onToolResult(chunk.toolName ?? '', chunk.data);
                            break;
                        case 'Error':
                            handlers.onError(chunk.message ?? 'Something went wrong.');
                            break;
                        case 'ConversationEnd':
                            handlers.onFinish();
                            break;
                    }
                },
                error: (err: unknown) => {
                    handlers.onError(err instanceof Error ? err.message : 'Something went wrong.');
                    resolve();
                },
                complete: () => resolve(),
            });
        });
    }

    async disconnect(): Promise<void> {
        if (this.connection) {
            const conn = this.connection;
            this.connection = null;
            // Swallow all errors during teardown — pending invocations are aborted
            // when the transport closes and will always throw a cancellation error.
            try { await conn.stop(); } catch { /* expected during teardown */ }
        }
    }
}

export const aiAssistantSignalrService = new AiAssistantSignalrService();
