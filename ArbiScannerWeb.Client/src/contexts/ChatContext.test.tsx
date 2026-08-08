import { describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { ChatContextProvider, useChatContext } from './ChatContext';

describe('ChatContextProvider / useChatContext', () => {
    it('throws when used outside a ChatContextProvider', () => {
        const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

        expect(() => renderHook(() => useChatContext())).toThrow(
            'useChatContext must be used within a ChatContextProvider',
        );

        consoleErrorSpy.mockRestore();
    });

    it('defaults currentSpreadId to null', () => {
        const wrapper = ({ children }: { children: ReactNode }) => <ChatContextProvider>{children}</ChatContextProvider>;

        const { result } = renderHook(() => useChatContext(), { wrapper });

        expect(result.current.currentSpreadId).toBeNull();
    });

    it('setCurrentSpreadId updates the value seen by consumers', () => {
        const wrapper = ({ children }: { children: ReactNode }) => <ChatContextProvider>{children}</ChatContextProvider>;

        const { result } = renderHook(() => useChatContext(), { wrapper });
        act(() => { result.current.setCurrentSpreadId('spread-123'); });

        expect(result.current.currentSpreadId).toBe('spread-123');
    });

    it('setCurrentSpreadId(null) clears a previously set value', () => {
        const wrapper = ({ children }: { children: ReactNode }) => <ChatContextProvider>{children}</ChatContextProvider>;

        const { result } = renderHook(() => useChatContext(), { wrapper });
        act(() => { result.current.setCurrentSpreadId('spread-123'); });
        act(() => { result.current.setCurrentSpreadId(null); });

        expect(result.current.currentSpreadId).toBeNull();
    });
});
