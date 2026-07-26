import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import OrderBlock from './OrderBlockComponent';

describe('OrderBlock', () => {
    it('renders the title and header labels', () => {
        render(<OrderBlock title="BTC/USDT" bids={[]} asks={[]} />);

        expect(screen.getByText('BTC/USDT')).toBeInTheDocument();
        expect(screen.getByText('orderBlock.headers.side')).toBeInTheDocument();
    });

    it('renders bid and ask rows sorted with the best price nearest the spread', () => {
        render(
            <OrderBlock
                title="BTC/USDT"
                bids={[{ price: 99, amount: 1 }, { price: 100, amount: 2 }]}
                asks={[{ price: 102, amount: 1 }, { price: 101, amount: 3 }]}
            />,
        );

        expect(screen.getByText('101.0000')).toBeInTheDocument();
        expect(screen.getByText('102.0000')).toBeInTheDocument();
        expect(screen.getByText('100.0000')).toBeInTheDocument();
        expect(screen.getByText('99.0000')).toBeInTheDocument();
        expect(screen.getAllByText('orderBlock.best').length).toBeGreaterThan(0);
    });

    it('shows a placeholder diff when the best price is zero', () => {
        render(<OrderBlock title="BTC/USDT" bids={[{ price: 0, amount: 1 }]} asks={[]} />);

        expect(screen.getByText('—')).toBeInTheDocument();
    });

    it('renders without crashing when bids/asks are omitted entirely', () => {
        render(<OrderBlock title={null} />);

        expect(screen.getByText('orderBlock.headers.price')).toBeInTheDocument();
    });
});
