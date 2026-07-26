import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SubscriptionElement from './SubcriptionElement';

const navigateMock = vi.fn();

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock };
});

describe('SubscriptionElement', () => {
    beforeEach(() => {
        navigateMock.mockClear();
    });

    it('renders the plan type and price', () => {
        render(<SubscriptionElement id={3} type="Pro" price={19.99} durationInDays={30} />);

        expect(screen.getByText('Pro')).toBeInTheDocument();
        expect(screen.getByText('19.99')).toBeInTheDocument();
    });

    it('navigates to the payment page with the subscription id on click', async () => {
        render(<SubscriptionElement id={3} type="Pro" price={19.99} durationInDays={30} />);

        await userEvent.click(screen.getByRole('button'));

        expect(navigateMock).toHaveBeenCalledWith('/en/payment?id=3', undefined);
    });
});
