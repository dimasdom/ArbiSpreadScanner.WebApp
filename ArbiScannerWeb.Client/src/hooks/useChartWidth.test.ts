import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useChartWidth } from './useChartWidth';

function setWindowWidth(width: number) {
    Object.defineProperty(window, 'innerWidth', { writable: true, configurable: true, value: width });
}

describe('useChartWidth', () => {
    const originalWidth = window.innerWidth;

    afterEach(() => {
        setWindowWidth(originalWidth);
    });

    it('returns desktopWidth when window is above the breakpoint', () => {
        setWindowWidth(1200);
        const { result } = renderHook(() => useChartWidth());
        expect(result.current).toBe(800);
    });

    it('returns a mobile-scaled width when window is at or below the breakpoint', () => {
        setWindowWidth(500);
        const { result } = renderHook(() => useChartWidth());
        expect(result.current).toBe(Math.floor(500 * 0.9));
    });

    it('returns a mobile-scaled width exactly at the breakpoint boundary (inclusive)', () => {
        setWindowWidth(768);
        const { result } = renderHook(() => useChartWidth());
        expect(result.current).toBe(Math.floor(768 * 0.9));
    });

    it('respects custom mobileWidth, desktopWidth, and breakpoint arguments', () => {
        setWindowWidth(400);
        const { result } = renderHook(() => useChartWidth(0.5, 1000, 600));
        expect(result.current).toBe(Math.floor(400 * 0.5));

        setWindowWidth(700);
        const { result: result2 } = renderHook(() => useChartWidth(0.5, 1000, 600));
        expect(result2.current).toBe(1000);
    });

    it('updates when the window is resized past the breakpoint', () => {
        setWindowWidth(1200);
        const { result } = renderHook(() => useChartWidth());
        expect(result.current).toBe(800);

        act(() => {
            setWindowWidth(400);
            window.dispatchEvent(new Event('resize'));
        });

        expect(result.current).toBe(Math.floor(400 * 0.9));
    });

    it('removes the resize listener on unmount', () => {
        const removeSpy = vi.spyOn(window, 'removeEventListener');
        const { unmount } = renderHook(() => useChartWidth());
        unmount();
        expect(removeSpy).toHaveBeenCalledWith('resize', expect.any(Function));
        removeSpy.mockRestore();
    });
});
