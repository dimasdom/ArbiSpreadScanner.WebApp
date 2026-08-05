import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { signalRService } from './signalrService';

function createMockConnection() {
    return {
        state: 'Disconnected' as string,
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        start: vi.fn().mockResolvedValue(undefined),
        stop: vi.fn().mockResolvedValue(undefined),
        invoke: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        off: vi.fn(),
    };
}

let mockConnection = createMockConnection();
const withUrlMock = vi.fn().mockReturnThis();

vi.mock('@microsoft/signalr', () => ({
    // A regular `function` (not an arrow function) is required here: the
    // real code invokes this with `new`, and arrow functions cannot be
    // constructors — using one throws "is not a constructor" at runtime.
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

describe('signalRService', () => {
    beforeEach(() => {
        mockConnection = createMockConnection();
        vi.mocked(HubConnectionBuilder).mockClear();
        withUrlMock.mockClear();
    });

    afterEach(async () => {
        await signalRService.disconnect();
    });

    it('connect() builds and starts a hub connection', async () => {
        await signalRService.connect('https://hub.test/ticker');

        expect(HubConnectionBuilder).toHaveBeenCalledTimes(1);
        expect(mockConnection.start).toHaveBeenCalledTimes(1);
    });

    it('connect() authenticates the hub via accessTokenFactory, not cookies', async () => {
        await signalRService.connect('https://hub.test/ticker');

        expect(withUrlMock).toHaveBeenCalledWith('https://hub.test/ticker', expect.objectContaining({
            accessTokenFactory: expect.any(Function),
        }));
        const [, options] = withUrlMock.mock.calls[0];
        await expect(options.accessTokenFactory()).resolves.toBe('the-token');
    });

    it('connect() registers reconnect and close handlers', async () => {
        await signalRService.connect('https://hub.test/ticker');

        expect(mockConnection.onreconnecting).toHaveBeenCalledWith(expect.any(Function));
        expect(mockConnection.onreconnected).toHaveBeenCalledWith(expect.any(Function));
        expect(mockConnection.onclose).toHaveBeenCalledWith(expect.any(Function));
    });

    it('connect() is a no-op when already connected', async () => {
        await signalRService.connect('https://hub.test/ticker');
        await signalRService.connect('https://hub.test/ticker');

        expect(HubConnectionBuilder).toHaveBeenCalledTimes(1);
    });

    it('isConnected() reflects the underlying connection state', async () => {
        expect(signalRService.isConnected()).toBe(false);

        await signalRService.connect('https://hub.test/ticker');
        expect(signalRService.isConnected()).toBe(false);

        mockConnection.state = 'Connected';
        expect(signalRService.isConnected()).toBe(true);
    });

    it('disconnect() stops the connection and clears state', async () => {
        await signalRService.connect('https://hub.test/ticker');

        await signalRService.disconnect();

        expect(mockConnection.stop).toHaveBeenCalledTimes(1);
        expect(signalRService.isConnected()).toBe(false);
    });

    it('disconnect() swallows stop() errors during teardown', async () => {
        await signalRService.connect('https://hub.test/ticker');
        mockConnection.stop.mockRejectedValue(new Error('aborted'));

        await expect(signalRService.disconnect()).resolves.toBeUndefined();
    });

    it('disconnect() is a no-op when never connected', async () => {
        await expect(signalRService.disconnect()).resolves.toBeUndefined();
    });

    it('joinGroup() throws when there is no active connection', async () => {
        await expect(signalRService.joinGroup('room-1')).rejects.toThrow('No SignalR connection');
    });

    it('joinGroup() invokes JoinGroup on the hub', async () => {
        await signalRService.connect('https://hub.test/ticker');

        await signalRService.joinGroup('room-1');

        expect(mockConnection.invoke).toHaveBeenCalledWith('JoinGroup', 'room-1');
    });

    it('leaveGroup() is a no-op when not connected', async () => {
        await signalRService.connect('https://hub.test/ticker');
        mockConnection.state = 'Disconnected';

        await signalRService.leaveGroup('room-1');

        expect(mockConnection.invoke).not.toHaveBeenCalled();
    });

    it('leaveGroup() invokes LeaveGroup when connected', async () => {
        await signalRService.connect('https://hub.test/ticker');
        mockConnection.state = 'Connected';

        await signalRService.leaveGroup('room-1');

        expect(mockConnection.invoke).toHaveBeenCalledWith('LeaveGroup', 'room-1');
    });

    it('leaveGroup() swallows cancellation errors from a race with disconnect', async () => {
        await signalRService.connect('https://hub.test/ticker');
        mockConnection.state = 'Connected';
        mockConnection.invoke.mockRejectedValue(
            new Error('Invocation canceled due to the underlying connection being closed.'),
        );

        await expect(signalRService.leaveGroup('room-1')).resolves.toBeUndefined();
    });

    it('leaveGroup() rethrows unrelated errors', async () => {
        await signalRService.connect('https://hub.test/ticker');
        mockConnection.state = 'Connected';
        mockConnection.invoke.mockRejectedValue(new Error('server exploded'));

        await expect(signalRService.leaveGroup('room-1')).rejects.toThrow('server exploded');
    });

    it('onTickerUpdate() throws when there is no active connection', () => {
        expect(() => signalRService.onTickerUpdate(() => {})).toThrow('No SignalR connection');
    });

    it('onTickerUpdate() registers a ReceiveMessage handler', async () => {
        await signalRService.connect('https://hub.test/ticker');
        const callback = vi.fn();

        signalRService.onTickerUpdate(callback);

        expect(mockConnection.on).toHaveBeenCalledWith('ReceiveMessage', callback);
    });

    it('offTickerUpdate() unregisters the ReceiveMessage handler', async () => {
        await signalRService.connect('https://hub.test/ticker');

        signalRService.offTickerUpdate();

        expect(mockConnection.off).toHaveBeenCalledWith('ReceiveMessage');
    });

    it('offTickerUpdate() is a no-op when there is no active connection', () => {
        expect(() => signalRService.offTickerUpdate()).not.toThrow();
    });
});
