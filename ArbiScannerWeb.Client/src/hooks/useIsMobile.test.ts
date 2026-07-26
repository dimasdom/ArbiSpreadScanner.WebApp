import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useIsMobile } from './useIsMobile';

function setWindowWidth(width: number) {
    Object.defineProperty(window, 'innerWidth', { writable: true, configurable: true, value: width });
}

describe('useIsMobile', () => {
    const originalWidth = window.innerWidth;

    afterEach(() => {
        setWindowWidth(originalWidth);
    });

    it.each([
        [600, true],
        [768, false],
        [1200, false],
    ])('returns %s for window width %i against the default breakpoint (768)', (width, expected) => {
        setWindowWidth(width);
        const { result } = renderHook(() => useIsMobile());
        expect(result.current).toBe(expected);
    });

    it('respects a custom breakpoint', () => {
        setWindowWidth(500);
        const { result } = renderHook(() => useIsMobile(600));
        expect(result.current).toBe(true);

        setWindowWidth(700);
        const { result: result2 } = renderHook(() => useIsMobile(600));
        expect(result2.current).toBe(false);
    });

    it('updates when window is resized below breakpoint', () => {
        setWindowWidth(1200);
        const { result } = renderHook(() => useIsMobile());
        expect(result.current).toBe(false);

        act(() => {
            setWindowWidth(400);
            window.dispatchEvent(new Event('resize'));
        });

        expect(result.current).toBe(true);
    });

    it('updates when window is resized above breakpoint', () => {
        setWindowWidth(400);
        const { result } = renderHook(() => useIsMobile());
        expect(result.current).toBe(true);

        act(() => {
            setWindowWidth(1024);
            window.dispatchEvent(new Event('resize'));
        });

        expect(result.current).toBe(false);
    });

    it('removes resize event listener on unmount', () => {
        const removeSpy = vi.spyOn(window, 'removeEventListener');
        const { unmount } = renderHook(() => useIsMobile());
        unmount();
        expect(removeSpy).toHaveBeenCalledWith('resize', expect.any(Function));
        removeSpy.mockRestore();
    });
});
