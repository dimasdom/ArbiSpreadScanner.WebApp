import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import ChatWidget from './ChatWidget';

interface CapturedHandlers {
    onStart: () => void;
    onDelta: (text: string) => void;
    onSpreadsData: (data: unknown) => void;
    onSpreadData: (data: unknown) => void;
    onRecommendedSpreads: (data: unknown) => void;
    onToolResult: (toolName: string, data: unknown) => void;
    onError: (message: string) => void;
    onFinish: () => void;
}

const streamMessageMock = vi.fn();
const disconnectMock = vi.fn().mockResolvedValue(undefined);
const useAuthMock = vi.fn();
const useChatContextMock = vi.fn();

let capturedHandlers: CapturedHandlers | null = null;
let resolveStream: (() => void) | null = null;

vi.mock('../services/aiAssistantSignalrService', () => ({
    aiAssistantSignalrService: {
        streamMessage: (...args: unknown[]) => streamMessageMock(...args),
        disconnect: (...args: unknown[]) => disconnectMock(...args),
    },
}));

vi.mock('react-oidc-context', () => ({
    useAuth: () => useAuthMock(),
}));

vi.mock('../contexts/ChatContext', () => ({
    useChatContext: () => useChatContextMock(),
}));

vi.mock('../services/loggerService', () => ({
    logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
}));

// jsdom doesn't implement scrollIntoView — ChatWidget calls it to keep the latest
// message in view.
Element.prototype.scrollIntoView = vi.fn();

function setWindowWidth(width: number) {
    Object.defineProperty(window, 'innerWidth', { writable: true, configurable: true, value: width });
}

async function openWidget() {
    render(<ChatWidget />, { wrapper: MemoryRouter });
    await userEvent.click(screen.getByRole('button', { name: 'widget.openAriaLabel' }));
}

async function sendPrompt(text: string) {
    await userEvent.type(screen.getByPlaceholderText('widget.inputPlaceholder'), text);
    await userEvent.click(screen.getByRole('button', { name: 'widget.send' }));
}

describe('ChatWidget', () => {
    const originalWidth = window.innerWidth;

    afterEach(() => {
        setWindowWidth(originalWidth);
    });

    beforeEach(() => {
        streamMessageMock.mockReset();
        disconnectMock.mockClear();
        capturedHandlers = null;
        resolveStream = null;
        // Defers indefinitely until a test manually drives capturedHandlers/resolveStream —
        // mirrors the real service, whose streamMessage() promise doesn't resolve until the
        // stream ends.
        streamMessageMock.mockImplementation((_request: unknown, handlers: CapturedHandlers) => {
            capturedHandlers = handlers;
            return new Promise<void>((resolve) => { resolveStream = resolve; });
        });
        useAuthMock.mockReturnValue({ isAuthenticated: true });
        useChatContextMock.mockReturnValue({ currentSpreadId: null });
    });

    it('renders nothing when the user is not authenticated', () => {
        useAuthMock.mockReturnValue({ isAuthenticated: false });

        const { container } = render(<ChatWidget />, { wrapper: MemoryRouter });

        expect(container).toBeEmptyDOMElement();
    });

    it('is closed by default and opens on button click, showing the greeting', async () => {
        render(<ChatWidget />, { wrapper: MemoryRouter });
        expect(screen.queryByText('widget.greeting')).not.toBeInTheDocument();

        await userEvent.click(screen.getByRole('button', { name: 'widget.openAriaLabel' }));

        expect(screen.getByText('widget.greeting')).toBeInTheDocument();
    });

    it('shows a thinking indicator immediately, then replaces it with streamed delta text', async () => {
        await openWidget();

        await sendPrompt('what does spread threshold mean?');

        expect(screen.getByRole('status', { name: 'Thinking' })).toBeInTheDocument();

        act(() => capturedHandlers?.onDelta('It is the minimum spread %'));

        expect(screen.queryByRole('status', { name: 'Thinking' })).not.toBeInTheDocument();
        expect(screen.getByText('It is the minimum spread %')).toBeInTheDocument();

        act(() => {
            capturedHandlers?.onDelta(' you want to be notified about.');
            resolveStream?.();
        });

        expect(await screen.findByText('It is the minimum spread % you want to be notified about.')).toBeInTheDocument();
    });

    it('sends the typed prompt with the current spread context id and renders SpreadsData as a preview box', async () => {
        useChatContextMock.mockReturnValue({ currentSpreadId: 'spread-42' });
        await openWidget();

        await sendPrompt('what are my spreads?');

        expect(streamMessageMock).toHaveBeenCalledWith(
            { prompt: 'what are my spreads?', spreadContextId: 'spread-42' }, expect.any(Object));

        act(() => {
            capturedHandlers?.onSpreadsData([{ positionModel: { guid: 'g1', symbol: 'BTC/USDT', spread: 1.5, exchangeLong: { exchange: 'Bybit' }, exchangeShort: { exchange: 'Binance' } }, tickers: [], groupName: 'BTC group' }]);
            resolveStream?.();
        });

        expect(await screen.findByText('BTC/USDT')).toBeInTheDocument();
    });

    it('renders SpreadData as an analysis box', async () => {
        await openWidget();

        await sendPrompt('analyze this spread');
        act(() => {
            capturedHandlers?.onSpreadData({
                positionModel: { guid: 'spread-1', symbol: 'ETH/USDT', spread: 1.2, exchangeLong: { exchange: 'Bybit' }, exchangeShort: { exchange: 'Binance' } },
                tickers: [],
                groupName: 'group',
                analysis: { recommended: true, reasons: ['Spread is 1.20%, at or above the 1% minimum.'] },
            });
            resolveStream?.();
        });

        expect(await screen.findByText('ETH/USDT')).toBeInTheDocument();
        expect(screen.getByText('widget.recommended')).toBeInTheDocument();
        expect(screen.getByText('Spread is 1.20%, at or above the 1% minimum.')).toBeInTheDocument();
    });

    it('renders RecommendedSpreads as a grouped recommendation box', async () => {
        await openWidget();

        await sendPrompt('what spread should I look at?');
        act(() => {
            capturedHandlers?.onRecommendedSpreads([
                {
                    category: 0,
                    details: {
                        positionModel: { guid: 'r1', symbol: 'SOL/USDT', spread: 3, exchangeLong: { exchange: 'Bybit' }, exchangeShort: { exchange: 'Binance' } },
                        tickers: [],
                        groupName: 'group',
                    },
                },
            ]);
            resolveStream?.();
        });

        expect(await screen.findByText('SOL/USDT')).toBeInTheDocument();
    });

    it('preserves leading text deltas that arrive before a tool result in the same turn', async () => {
        await openWidget();

        await sendPrompt('what spread should I look at?');
        act(() => capturedHandlers?.onDelta('Here you go.'));
        act(() => {
            capturedHandlers?.onRecommendedSpreads([
                {
                    category: 0,
                    details: {
                        positionModel: { guid: 'r1', symbol: 'SOL/USDT', spread: 3, exchangeLong: { exchange: 'Bybit' }, exchangeShort: { exchange: 'Binance' } },
                        tickers: [],
                        groupName: 'group',
                    },
                },
            ]);
            resolveStream?.();
        });

        expect(screen.getByText('Here you go.')).toBeInTheDocument();
        expect(await screen.findByText('SOL/USDT')).toBeInTheDocument();
    });

    it('closes the widget on mobile when a link inside a message is clicked', async () => {
        setWindowWidth(500);
        await openWidget();

        await sendPrompt('what spread should I look at?');
        act(() => {
            capturedHandlers?.onRecommendedSpreads([
                {
                    category: 0,
                    details: {
                        positionModel: { guid: 'r1', symbol: 'SOL/USDT', spread: 3, exchangeLong: { exchange: 'Bybit' }, exchangeShort: { exchange: 'Binance' } },
                        tickers: [],
                        groupName: 'group',
                    },
                },
            ]);
            resolveStream?.();
        });

        const link = await screen.findByRole('link', { name: 'widget.viewSpread' });
        await userEvent.click(link);

        expect(screen.queryByText('widget.greeting')).not.toBeInTheDocument();
    });

    it('keeps the widget open on desktop when a link inside a message is clicked', async () => {
        setWindowWidth(1200);
        await openWidget();

        await sendPrompt('what spread should I look at?');
        act(() => {
            capturedHandlers?.onRecommendedSpreads([
                {
                    category: 0,
                    details: {
                        positionModel: { guid: 'r1', symbol: 'SOL/USDT', spread: 3, exchangeLong: { exchange: 'Bybit' }, exchangeShort: { exchange: 'Binance' } },
                        tickers: [],
                        groupName: 'group',
                    },
                },
            ]);
            resolveStream?.();
        });

        const link = await screen.findByRole('link', { name: 'widget.viewSpread' });
        await userEvent.click(link);

        expect(screen.getByText('widget.greeting')).toBeInTheDocument();
    });

    it('renders a fallback ToolResult chunk as JSON', async () => {
        await openWidget();

        await sendPrompt('what are my spreads?');
        act(() => {
            capturedHandlers?.onToolResult('some_future_tool', [{ groupName: 'BTC group' }]);
            resolveStream?.();
        });

        expect(await screen.findByText(/BTC group/)).toBeInTheDocument();
    });

    it('clears the thinking indicator on onFinish even if no delta/data/error chunk ever arrived', async () => {
        await openWidget();

        await sendPrompt('hello');
        expect(screen.getByRole('status', { name: 'Thinking' })).toBeInTheDocument();

        act(() => {
            capturedHandlers?.onFinish();
            resolveStream?.();
        });

        expect(screen.queryByRole('status', { name: 'Thinking' })).not.toBeInTheDocument();
    });

    it('renders an Error chunk using its message, styled as an error', async () => {
        await openWidget();

        await sendPrompt('what are my spreads?');
        act(() => {
            capturedHandlers?.onError('Your session has expired - please sign in again.');
            resolveStream?.();
        });

        expect(await screen.findByText('Your session has expired - please sign in again.')).toBeInTheDocument();
    });

    it('renders a generic error message when the hub call rejects', async () => {
        streamMessageMock.mockRejectedValue(new Error('connection lost'));
        await openWidget();

        await sendPrompt('what are my spreads?');

        expect(await screen.findByText('widget.genericError')).toBeInTheDocument();
    });

    it('shows a spread-context suggestion when opened with a current spread and no messages yet', async () => {
        useChatContextMock.mockReturnValue({ currentSpreadId: 'spread-42' });
        await openWidget();

        const suggestion = screen.getByRole('button', { name: 'widget.spreadContextSuggestion' });
        await userEvent.click(suggestion);

        expect(streamMessageMock).toHaveBeenCalledWith(
            { prompt: 'widget.spreadContextPrompt', spreadContextId: 'spread-42' }, expect.any(Object));
    });

    it('does not show the spread-context suggestion when there is no current spread', async () => {
        await openWidget();

        expect(screen.queryByRole('button', { name: 'widget.spreadContextSuggestion' })).not.toBeInTheDocument();
    });

    it('disconnects the AI assistant hub connection on unmount', () => {
        const { unmount } = render(<ChatWidget />);

        unmount();

        expect(disconnectMock).toHaveBeenCalledTimes(1);
    });
});
