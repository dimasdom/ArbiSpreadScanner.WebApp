import {
    HubConnection,
    HubConnectionBuilder,
    LogLevel,
} from '@microsoft/signalr';
import type { MessageDTO } from '../types/tickerType';
import { logger } from './loggerService';
import { getAccessToken } from './oidcUserManager';


class SignalRService {
    private connection: HubConnection | null = null;

    async connect(hubUrl: string): Promise<void> {
        if (this.connection) return;

        // Browsers can't set an Authorization header on a WebSocket handshake —
        // accessTokenFactory instead appends the token as an access_token query
        // param, which the API's JWT bearer handler only honors for /hubs paths.
        this.connection = new HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: async () => (await getAccessToken()) ?? '',
            })
            .configureLogging(LogLevel.Information)
            .withAutomaticReconnect()
            .build();

        this.connection.onreconnecting(() => {
            logger.warn('SignalR reconnecting', 'signalrService');
        });

        this.connection.onreconnected(() => {
            logger.info('SignalR reconnected', 'signalrService');
        });

        this.connection.onclose(() => {
            logger.warn('SignalR disconnected', 'signalrService');
        });

        await this.connection.start();
        logger.info('SignalR connected', 'signalrService');
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

    async joinGroup(groupName: string): Promise<void> {
        if (!this.connection) throw new Error('No SignalR connection');
        await this.connection.invoke('JoinGroup', groupName);
        logger.debug(`Joined group: ${groupName}`, 'signalrService');
    }

    async leaveGroup(groupName: string): Promise<void> {
        if (this.connection?.state !== 'Connected') return;

        const connection = this.connection;
        try {
            await connection.invoke('LeaveGroup', groupName);
            logger.debug(`Left group: ${groupName}`, 'signalrService');
        } catch (err: unknown) {
            // Swallow cancellation errors that occur when leaveGroup races with disconnect
            const msg: string = (err as Error)?.message ?? '';
            if (!msg.includes('canceled') && !msg.includes('connection being closed')) {
                throw err;
            }
        }
    }

    onTickerUpdate(callback: (data: MessageDTO) => void): void {
        if (!this.connection) throw new Error('No SignalR connection');
        this.connection.on('ReceiveMessage', callback);
    }

    offTickerUpdate(): void {
        if (!this.connection) return;
        this.connection.off('ReceiveMessage');
    }

    isConnected(): boolean {
        return !!this.connection && this.connection.state === 'Connected';
    }
}

export const signalRService = new SignalRService();
