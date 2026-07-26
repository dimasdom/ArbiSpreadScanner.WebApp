import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PaymentSuccessPage from './PaymentSuccessPage';

const navigateMock = vi.fn();

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock };
});

describe('PaymentSuccessPage', () => {
    beforeEach(() => {
        navigateMock.mockClear();
    });

    it('renders the success heading', () => {
        render(<PaymentSuccessPage />);

        expect(screen.getByText('payment.success.heading')).toBeInTheDocument();
    });

    it('navigates home when the CTA button is clicked', async () => {
        render(<PaymentSuccessPage />);

        await userEvent.click(screen.getByRole('button', { name: 'payment.success.startTradingButton' }));

        expect(navigateMock).toHaveBeenCalledWith('/en/', undefined);
    });
});
