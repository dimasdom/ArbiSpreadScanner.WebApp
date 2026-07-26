import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ErrorState from './ErrorState';

describe('ErrorState', () => {
    it('renders a default message when none is provided', () => {
        render(<ErrorState />);

        expect(screen.getByText('errorState.default')).toBeInTheDocument();
    });

    it('renders a custom message when provided', () => {
        render(<ErrorState message="Something specific broke" />);

        expect(screen.getByText('Something specific broke')).toBeInTheDocument();
    });

    it('does not render a retry button when onRetry is omitted', () => {
        render(<ErrorState />);

        expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('invokes onRetry when the retry button is clicked', async () => {
        const onRetry = vi.fn();
        render(<ErrorState onRetry={onRetry} />);

        await userEvent.click(screen.getByRole('button'));

        expect(onRetry).toHaveBeenCalledOnce();
    });
});
