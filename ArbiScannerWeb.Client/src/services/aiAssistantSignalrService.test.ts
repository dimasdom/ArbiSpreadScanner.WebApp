import { beforeEach, describe, expect, it, vi } from 'vitest';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { aiAssistantSignalrService } from './aiAssistantSignalrService';

interface StreamObserver {
    next: (value: unknown) => void;
    error: (err: unknown) => void;
    complete: () => void;
}

function createStreamResult(chunks: unknown[], streamError?: Error) {
    return {
        subscribe: (observer: StreamObserver) => {
            queueMicrotask(() => {
                for (const chunk of chunks) observer.next(chunk);
                if (streamError) observer.error(streamError);
                else observer.complete();
            });
            return { dispose: vi.fn() };
        },
    };
}

function createMockConnection() {
    return {
        state: 'Disconnected' as string,
        onclose: vi.fn(),
        start: vi.fn().mockResolvedValue(undefined),
        stop: vi.fn().mockResolvedValue(undefined),
        stream: vi.fn().mockReturnValue(createStreamResult([{ kind: 'AnswerDelta', textDelta: 'not supported' }])),
    };
}

function createHandlers() {
    return {
        onStart: vi.fn(),
        onDelta: vi.fn(),
        onSpreadsData: vi.fn(),
        onSpreadData: vi.fn(),
        onRecommendedSpreads: vi.fn(),
        onToolResult: vi.fn(),
        onError: vi.fn(),
        onFinish: vi.fn(),
    };
}

let mockConnection = createMockConnection();
const withUrlMock = vi.fn().mockReturnThis();

vi.mock('@microsoft/signalr', () => ({
    // A regular `function` (not an arrow function) is required here: the real code
    // invokes this with `new`, and arrow functions cannot be constructors.
    HubConnectionBuilder: vi.fn().mockImplementation(function HubConnectionBuilderMock() {
        return {
            withUrl: withUrlMock,
            configureLogging: vi.fn().mockReturnThis(),
            withAutomaticReconnect: vi.fn().mockReturnThis(),
            build: vi.fn(() => mockConnection),
        };
    }),
    LogLevel: { Information: 1 },
}));

vi.mock('./loggerService', () => ({
    logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
}));

vi.mock('./oidcUserManager', () => ({
    getAccessToken: vi.fn().mockResolvedValue('the-token'),
}));

describe('aiAssistantSignalrService', () => {
    beforeEach(async () => {
        mockConnection = createMockConnection();
        vi.mocked(HubConnectionBuilder).mockClear();
        withUrlMock.mockClear();
        await aiAssistantSignalrService.disconnect();
    });

    it('streamMessage() connects to /ai-hub/chat on first use', async () => {
        await aiAssistantSignalrService.streamMessage({ prompt: 'what are my spreads?' }, createHandlers());

        expect(HubConnectionBuilder).toHaveBeenCalledTimes(1);
        expect(withUrlMock).toHaveBeenCalledWith('/ai-hub/chat', expect.objectContaining({
            accessTokenFactory: expect.any(Function),
        }));
        expect(mockConnection.start).toHaveBeenCalledTimes(1);
    });

    it('streamMessage() authenticates via accessTokenFactory, not cookies', async () => {
        await aiAssistantSignalrService.streamMessage({ prompt: 'what are my spreads?' }, createHandlers());

        const [, options] = withUrlMock.mock.calls[0];
        await expect(options.accessTokenFactory()).resolves.toBe('the-token');
    });

    it('streamMessage() invokes the StreamMessage hub method with the request', async () => {
        await aiAssistantSignalrService.streamMessage(
            { prompt: 'what are my spreads?', spreadContextId: 'spread-1' }, createHandlers());

        expect(mockConnection.stream).toHaveBeenCalledWith(
            'StreamMessage', { prompt: 'what are my spreads?', spreadContextId: 'spread-1' });
    });

    it('streamMessage() forwards AnswerDelta chunks to onDelta in arrival order', async () => {
        mockConnection.stream.mockReturnValue(createStreamResult([
            { kind: 'AnswerDelta', textDelta: 'It is the minimum spread %' },
            { kind: 'AnswerDelta', textDelta: ' you want to be notified about.' },
        ]));
        const handlers = createHandlers();

        await aiAssistantSignalrService.streamMessage({ prompt: 'what does spread threshold mean?' }, handlers);

        expect(handlers.onDelta).toHaveBeenNthCalledWith(1, 'It is the minimum spread %');
        expect(handlers.onDelta).toHaveBeenNthCalledWith(2, ' you want to be notified about.');
    });

    it('streamMessage() forwards a ConversationStart chunk to onStart and a ConversationEnd chunk to onFinish', async () => {
        mockConnection.stream.mockReturnValue(createStreamResult([
            { kind: 'ConversationStart' },
            { kind: 'AnswerDelta', textDelta: 'Hi' },
            { kind: 'ConversationEnd' },
        ]));
        const handlers = createHandlers();

        await aiAssistantSignalrService.streamMessage({ prompt: 'hello' }, handlers);

        expect(handlers.onStart).toHaveBeenCalledTimes(1);
        expect(handlers.onFinish).toHaveBeenCalledTimes(1);
    });

    it('streamMessage() forwards a SpreadsData chunk to onSpreadsData', async () => {
        mockConnection.stream.mockReturnValue(createStreamResult([
            { kind: 'SpreadsData', toolName: 'get_spreads_for_current_user', data: [{ groupName: 'BTC group' }] },
        ]));
        const handlers = createHandlers();

        await aiAssistantSignalrService.streamMessage({ prompt: 'what are my spreads?' }, handlers);

        expect(handlers.onSpreadsData).toHaveBeenCalledWith([{ groupName: 'BTC group' }]);
        expect(handlers.onDelta).not.toHaveBeenCalled();
    });

    it('streamMessage() forwards a SpreadData chunk to onSpreadData', async () => {
        mockConnection.stream.mockReturnValue(createStreamResult([
            { kind: 'SpreadData', toolName: 'get_spread_by_id', data: { id: 'spread-1' } },
        ]));
        const handlers = createHandlers();

        await aiAssistantSignalrService.streamMessage({ prompt: 'analyze this spread' }, handlers);

        expect(handlers.onSpreadData).toHaveBeenCalledWith({ id: 'spread-1' });
    });

    it('streamMessage() forwards a RecommendedSpreads chunk to onRecommendedSpreads', async () => {
        mockConnection.stream.mockReturnValue(createStreamResult([
            { kind: 'RecommendedSpreads', toolName: 'get_recommended_spreads', data: [{ category: 0 }] },
        ]));
        const handlers = createHandlers();

        await aiAssistantSignalrService.streamMessage({ prompt: 'what spread should I look at?' }, handlers);

        expect(handlers.onRecommendedSpreads).toHaveBeenCalledWith([{ category: 0 }]);
    });

    it('streamMessage() forwards a ToolResult chunk to onToolResult', async () => {
        mockConnection.stream.mockReturnValue(createStreamResult([
            { kind: 'ToolResult', toolName: 'some_future_tool', data: [{ groupName: 'BTC group' }] },
        ]));
        const handlers = createHandlers();

        await aiAssistantSignalrService.streamMessage({ prompt: 'what are my spreads?' }, handlers);

        expect(handlers.onToolResult).toHaveBeenCalledWith('some_future_tool', [{ groupName: 'BTC group' }]);
        expect(handlers.onDelta).not.toHaveBeenCalled();
    });

    it('streamMessage() forwards an Error chunk to onError', async () => {
        mockConnection.stream.mockReturnValue(createStreamResult([{ kind: 'Error', message: 'Authentication failed.' }]));
        const handlers = createHandlers();

        await aiAssistantSignalrService.streamMessage({ prompt: 'what are my spreads?' }, handlers);

        expect(handlers.onError).toHaveBeenCalledWith('Authentication failed.');
    });

    it('streamMessage() calls onError and still resolves when the stream itself fails', async () => {
        mockConnection.stream.mockReturnValue(createStreamResult([], new Error('transport dropped')));
        const handlers = createHandlers();

        await expect(aiAssistantSignalrService.streamMessage({ prompt: 'what are my spreads?' }, handlers))
            .resolves.toBeUndefined();
        expect(handlers.onError).toHaveBeenCalledWith('transport dropped');
    });

    it('streamMessage() reuses an already-connected connection without reconnecting', async () => {
        mockConnection.state = 'Connected';
        await aiAssistantSignalrService.streamMessage({ prompt: 'first' }, createHandlers());

        await aiAssistantSignalrService.streamMessage({ prompt: 'second' }, createHandlers());

        expect(HubConnectionBuilder).toHaveBeenCalledTimes(1);
        expect(mockConnection.start).toHaveBeenCalledTimes(1);
    });

    it('disconnect() stops the connection so the next streamMessage reconnects', async () => {
        await aiAssistantSignalrService.streamMessage({ prompt: 'what are my spreads?' }, createHandlers());

        await aiAssistantSignalrService.disconnect();
        await aiAssistantSignalrService.streamMessage({ prompt: 'again' }, createHandlers());

        expect(mockConnection.stop).toHaveBeenCalledTimes(1);
        expect(HubConnectionBuilder).toHaveBeenCalledTimes(2);
    });

    it('disconnect() swallows stop() errors during teardown', async () => {
        await aiAssistantSignalrService.streamMessage({ prompt: 'what are my spreads?' }, createHandlers());
        mockConnection.stop.mockRejectedValue(new Error('aborted'));

        await expect(aiAssistantSignalrService.disconnect()).resolves.toBeUndefined();
    });

    it('disconnect() is a no-op when never connected', async () => {
        await expect(aiAssistantSignalrService.disconnect()).resolves.toBeUndefined();
    });
});
