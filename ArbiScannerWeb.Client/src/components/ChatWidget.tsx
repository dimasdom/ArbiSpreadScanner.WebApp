import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from 'react-oidc-context';
import { aiAssistantSignalrService } from '../services/aiAssistantSignalrService';
import { useChatContext } from '../contexts/ChatContext';
import { logger } from '../services/loggerService';
import { useIsMobile } from '../hooks/useIsMobile';
import SpreadRecommendationBox from './SpreadRecommendationBox';
import SpreadsPreviewBox from './SpreadsPreviewBox';
import SpreadAnalysisBox from './SpreadAnalysisBox';
import type { RecommendedSpreadDTO, TradeOpportunityDetailsDTO } from '../types/tradeOpportunityModel';

// How many of get_spreads_for_current_user's results to show as cards before pointing
// the user to the full spreads page for the rest.
const SPREADS_PREVIEW_COUNT = 3;

type ChatMessagePayload =
    | { kind: 'recommendedSpreads'; data: RecommendedSpreadDTO[] }
    | { kind: 'spreadsPreview'; data: TradeOpportunityDetailsDTO[]; totalCount: number }
    | { kind: 'spreadAnalysis'; data: TradeOpportunityDetailsDTO };

interface ChatMessage {
    id: string;
    role: 'user' | 'assistant';
    text: string;
    isError?: boolean;
    /** True from the moment the request is sent until the first content arrives - renders
     * the thinking indicator instead of (empty) text, so the user knows it's still working. */
    isPending?: boolean;
    /** Structured tool data (spreads/recommendations/analysis) rendered as a rich box
     * alongside `text` - a model turn can emit a short leading delta before deciding to
     * call a tool, so the two are additive, not mutually exclusive. */
    payload?: ChatMessagePayload;
}

let nextMessageId = 0;

const ThinkingIndicator: React.FC = () => (
    <output className="flex items-center gap-1 py-1" aria-label="Thinking">
        <span className="h-2 w-2 rounded-full bg-gray-400 dark:bg-gray-500 animate-bounce [animation-delay:-0.3s]" />
        <span className="h-2 w-2 rounded-full bg-gray-400 dark:bg-gray-500 animate-bounce [animation-delay:-0.15s]" />
        <span className="h-2 w-2 rounded-full bg-gray-400 dark:bg-gray-500 animate-bounce" />
    </output>
);

function getMessageBubbleClass(message: ChatMessage): string {
    if (message.role === 'user') return 'bg-indigo-600 text-white ml-8';
    if (message.isError) return 'bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-300 mr-8';
    return 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-100 mr-8';
}

function renderPayload(payload: ChatMessagePayload) {
    switch (payload.kind) {
        case 'recommendedSpreads':
            return <SpreadRecommendationBox spreads={payload.data} />;
        case 'spreadsPreview':
            return <SpreadsPreviewBox spreads={payload.data} totalCount={payload.totalCount} />;
        case 'spreadAnalysis':
            return <SpreadAnalysisBox spread={payload.data} />;
    }
}

function renderMessageContent(message: ChatMessage) {
    if (message.isPending && !message.text && !message.payload) return <ThinkingIndicator />;
    if (!message.payload) return message.text;

    // A turn can stream some leading prose before deciding to call a tool - keep both
    // instead of letting the tool result blank out whatever text already arrived.
    return (
        <div className="space-y-2">
            {message.text && <p>{message.text}</p>}
            {renderPayload(message.payload)}
        </div>
    );
}

// A floating widget, mounted once at the App root (same pattern as
// CookieConsentModal) so it survives route navigation within one browser
// session. There is no server-side conversation history — this component's
// `messages` state is purely a local, in-memory transcript for display; each
// call to ArbiScanner.AiAssistant is a fresh, self-contained request.
const ChatWidget: React.FC = () => {
    const { t } = useTranslation('chat');
    const auth = useAuth();
    const { currentSpreadId } = useChatContext();
    // 640px matches this component's own `sm:` breakpoint, below which the panel is
    // fixed inset-0 (full-screen) - so a link's navigation is invisible behind it
    // unless the widget also closes.
    const isMobile = useIsMobile(640);
    const [isOpen, setIsOpen] = useState(false);
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [input, setInput] = useState('');
    const [isSending, setIsSending] = useState(false);
    const messagesEndRef = useRef<HTMLDivElement>(null);
    const messagesContainerRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages, isOpen]);

    // On mobile the panel covers the whole screen, so navigating to a spread/spreads-page
    // link from inside the chat would otherwise happen invisibly behind it. Closing on
    // click makes the resulting page transition visible and tells the user something
    // happened. Desktop's floating panel already leaves the rest of the page visible, so
    // it's left open there. Attached imperatively (rather than a JSX onClick on the
    // container div) since this only delegates to genuinely interactive descendants
    // (links), which are already keyboard-operable on their own.
    useEffect(() => {
        if (!isMobile || !isOpen) return;
        const container = messagesContainerRef.current;
        if (!container) return;

        const handleClick = (event: MouseEvent) => {
            if (event.target instanceof HTMLElement && event.target.closest('a')) {
                setIsOpen(false);
            }
        };
        container.addEventListener('click', handleClick);
        return () => container.removeEventListener('click', handleClick);
    }, [isMobile, isOpen]);

    useEffect(() => () => {
        aiAssistantSignalrService.disconnect().catch(() => { /* teardown */ });
    }, []);

    if (!auth.isAuthenticated) return null;

    const appendMessage = (message: ChatMessage) => {
        setMessages((prev) => [...prev, message]);
    };

    const patchMessage = (id: string, patch: Partial<ChatMessage>) => {
        setMessages((prev) => prev.map((message) => (message.id === id ? { ...message, ...patch } : message)));
    };

    const send = async (prompt: string) => {
        const trimmed = prompt.trim();
        if (!trimmed || isSending) return;

        appendMessage({ id: String(nextMessageId++), role: 'user', text: trimmed });
        setInput('');
        setIsSending(true);

        const assistantId = String(nextMessageId++);
        appendMessage({ id: assistantId, role: 'assistant', text: '', isPending: true });

        try {
            await aiAssistantSignalrService.streamMessage(
                { prompt: trimmed, spreadContextId: currentSpreadId },
                {
                    // The client already shows the thinking indicator optimistically from
                    // the moment it sends the request - nothing to react to here yet.
                    onStart: () => {},
                    onDelta: (text) => {
                        setMessages((prev) => prev.map((message) => (
                            message.id === assistantId
                                ? { ...message, text: message.text + text, isPending: false }
                                : message
                        )));
                    },
                    onSpreadsData: (data) => {
                        const spreads = (Array.isArray(data) ? data : []) as TradeOpportunityDetailsDTO[];
                        patchMessage(assistantId, {
                            isPending: false,
                            payload: {
                                kind: 'spreadsPreview',
                                data: spreads.slice(0, SPREADS_PREVIEW_COUNT),
                                totalCount: spreads.length,
                            },
                        });
                    },
                    onSpreadData: (data) => {
                        patchMessage(assistantId, {
                            isPending: false,
                            payload: { kind: 'spreadAnalysis', data: data as TradeOpportunityDetailsDTO },
                        });
                    },
                    onRecommendedSpreads: (data) => {
                        const spreads = (Array.isArray(data) ? data : []) as RecommendedSpreadDTO[];
                        patchMessage(assistantId, {
                            isPending: false,
                            payload: { kind: 'recommendedSpreads', data: spreads },
                        });
                    },
                    onToolResult: (_toolName, data) => {
                        patchMessage(assistantId, { text: JSON.stringify(data, null, 2), isPending: false });
                    },
                    onError: (message) => {
                        patchMessage(assistantId, { text: message, isError: true, isPending: false });
                    },
                    // Guarantees the thinking indicator is dismissed even if a response
                    // somehow arrives without a delta/data/error chunk (e.g. an empty
                    // free-text answer) - the previous implementation relied solely on those
                    // chunks to flip isPending, which could otherwise leave it stuck forever.
                    onFinish: () => {
                        patchMessage(assistantId, { isPending: false });
                    },
                },
            );
        } catch (error: unknown) {
            const details = error instanceof Error ? error.message : 'Unknown chat error';
            logger.error('AI assistant request failed', 'ChatWidget', details);
            patchMessage(assistantId, { text: t('widget.genericError'), isError: true, isPending: false });
        } finally {
            setIsSending(false);
        }
    };

    const handleSubmit = (event: React.FormEvent) => {
        event.preventDefault();
        void send(input);
    };

    return (
        <>
            <button
                type="button"
                aria-label={isOpen ? t('widget.closeAriaLabel') : t('widget.openAriaLabel')}
                onClick={() => setIsOpen((prev) => !prev)}
                className={`fixed bottom-6 right-6 z-70 h-14 w-14 items-center justify-center rounded-full bg-indigo-600 text-2xl shadow-lg hover:bg-indigo-700 transition-colors ${
                    isOpen ? 'hidden sm:flex' : 'flex'
                }`}
            >
                {isOpen ? '✕' : '🤖'}
            </button>

            {isOpen && (
                <div className="fixed inset-0 z-65 flex flex-col bg-white dark:bg-gray-900 sm:inset-auto sm:bottom-24 sm:right-6 sm:h-[32rem] sm:w-96 sm:rounded-2xl sm:shadow-2xl sm:border sm:border-gray-200 dark:sm:border-gray-800">
                    <div className="flex items-center justify-between border-b border-gray-200 dark:border-gray-800 px-4 py-3">
                        <h2 className="font-semibold text-gray-800 dark:text-gray-100">{t('widget.title')}</h2>
                        <button
                            type="button"
                            aria-label={t('widget.closeAriaLabel')}
                            onClick={() => setIsOpen(false)}
                            className="text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 sm:hidden"
                        >
                            ✕
                        </button>
                    </div>

                    <div ref={messagesContainerRef} className="flex-1 overflow-y-auto px-4 py-3 space-y-3">
                        <div className="rounded-lg bg-indigo-50 dark:bg-indigo-950 text-indigo-900 dark:text-indigo-100 text-sm p-3">
                            {t('widget.greeting')}
                        </div>

                        {currentSpreadId && messages.length === 0 && (
                            <button
                                type="button"
                                onClick={() => void send(t('widget.spreadContextPrompt'))}
                                className="text-sm px-3 py-2 rounded-full border border-indigo-300 text-indigo-700 dark:text-indigo-300 dark:border-indigo-700 hover:bg-indigo-50 dark:hover:bg-indigo-950 transition-colors"
                            >
                                {t('widget.spreadContextSuggestion')}
                            </button>
                        )}

                        {messages.map((message) => (
                            <div
                                key={message.id}
                                className={`text-sm rounded-lg p-3 whitespace-pre-wrap break-words ${getMessageBubbleClass(message)}`}
                            >
                                {renderMessageContent(message)}
                            </div>
                        ))}
                        <div ref={messagesEndRef} />
                    </div>

                    <form onSubmit={handleSubmit} className="border-t border-gray-200 dark:border-gray-800 p-3 flex gap-2">
                        <input
                            type="text"
                            value={input}
                            onChange={(event) => setInput(event.target.value)}
                            placeholder={t('widget.inputPlaceholder')}
                            disabled={isSending}
                            className="flex-1 rounded-full border border-gray-300 dark:border-gray-700 bg-transparent px-4 py-2 text-sm text-gray-800 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                        />
                        <button
                            type="submit"
                            disabled={isSending || !input.trim()}
                            className="rounded-full bg-indigo-600 text-white px-4 py-2 text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
                        >
                            {t('widget.send')}
                        </button>
                    </form>
                </div>
            )}
        </>
    );
};

export default ChatWidget;
