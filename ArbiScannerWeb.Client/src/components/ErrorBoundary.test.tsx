import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ErrorBoundary from './ErrorBoundary';

vi.mock('../services/loggerService', () => ({
    logger: { error: vi.fn(), warn: vi.fn(), info: vi.fn(), debug: vi.fn() },
}));

function BrokenComponent(): never {
    throw new Error('Test render error');
}

describe('ErrorBoundary', () => {
    beforeEach(() => {
        vi.spyOn(console, 'error').mockImplementation(() => {});
    });

    it('renders children when no error is thrown', () => {
        render(
            <ErrorBoundary>
                <p>All good</p>
            </ErrorBoundary>,
        );
        expect(screen.getByText('All good')).toBeInTheDocument();
    });

    it('displays fallback UI when a child throws', () => {
        render(
            <ErrorBoundary>
                <BrokenComponent />
            </ErrorBoundary>,
        );
        expect(screen.getByText('Something went wrong')).toBeInTheDocument();
        expect(screen.getByText(/unexpected error/i)).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /refresh/i })).toBeInTheDocument();
    });

    it('does not render children after an error', () => {
        render(
            <ErrorBoundary>
                <BrokenComponent />
            </ErrorBoundary>,
        );
        expect(screen.queryByText('All good')).not.toBeInTheDocument();
    });

    it('calls window.location.reload when Refresh is clicked', async () => {
        const user = userEvent.setup();
        const reloadSpy = vi.fn();
        Object.defineProperty(window, 'location', {
            value: { reload: reloadSpy },
            writable: true,
            configurable: true,
        });

        render(
            <ErrorBoundary>
                <BrokenComponent />
            </ErrorBoundary>,
        );

        await user.click(screen.getByRole('button', { name: /refresh/i }));
        expect(reloadSpy).toHaveBeenCalledOnce();
    });
});
