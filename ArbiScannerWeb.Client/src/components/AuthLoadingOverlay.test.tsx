import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import AuthLoadingOverlay, { AUTH_LOADING_OVERLAY_TRANSITION_MS } from './AuthLoadingOverlay';

describe('AuthLoadingOverlay', () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('renders the localized message and a spinner below it when shown', () => {
        render(<AuthLoadingOverlay show />);

        const overlay = screen.getByRole('status');
        expect(overlay).toBeInTheDocument();
        expect(screen.getByText('loading.tradingOpportunities')).toBeInTheDocument();
        expect(overlay.querySelector('.animate-spin')).not.toBeNull();
    });

    it('renders nothing when never shown', () => {
        render(<AuthLoadingOverlay show={false} />);

        expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });

    it('is fully opaque while shown', () => {
        render(<AuthLoadingOverlay show />);

        expect(screen.getByRole('status')).toHaveClass('opacity-100');
    });

    it('fades out and is removed from the DOM only after the transition completes', () => {
        const { rerender } = render(<AuthLoadingOverlay show />);

        rerender(<AuthLoadingOverlay show={false} />);

        const overlay = screen.getByRole('status');
        expect(overlay).toHaveClass('opacity-0');

        act(() => {
            vi.advanceTimersByTime(AUTH_LOADING_OVERLAY_TRANSITION_MS - 1);
        });
        expect(screen.getByRole('status')).toBeInTheDocument();

        act(() => {
            vi.advanceTimersByTime(1);
        });
        expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });

    it('cancels a pending removal and stays visible if shown again mid-transition', () => {
        const { rerender } = render(<AuthLoadingOverlay show />);

        rerender(<AuthLoadingOverlay show={false} />);
        act(() => {
            vi.advanceTimersByTime(AUTH_LOADING_OVERLAY_TRANSITION_MS / 2);
        });

        rerender(<AuthLoadingOverlay show />);
        act(() => {
            vi.advanceTimersByTime(AUTH_LOADING_OVERLAY_TRANSITION_MS);
        });

        expect(screen.getByRole('status')).toHaveClass('opacity-100');
    });
});
