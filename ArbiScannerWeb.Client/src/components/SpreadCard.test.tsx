import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import SpreadCard from './SpreadCard';
import { createEmptyPossiblePositionModel } from '../types/tradeOpportunityModel';
import type { TradeOpportunityDetailsDTO } from '../types/tradeOpportunityModel';

function makeDto(overrides: Partial<TradeOpportunityDetailsDTO['positionModel']> = {}): TradeOpportunityDetailsDTO {
    const positionModel = {
        ...createEmptyPossiblePositionModel(),
        guid: 'spread-1',
        symbol: 'BTC/USDT',
        spread: 2.5,
        ...overrides,
    };
    positionModel.exchangeLong = { ...positionModel.exchangeLong, exchange: 'Bybit' };
    positionModel.exchangeShort = { ...positionModel.exchangeShort, exchange: 'Binance' };

    return { positionModel, tickers: [], groupName: 'group', analysis: null };
}

describe('SpreadCard', () => {
    it('renders the symbol, spread pill, exchanges and a link to the spread page', () => {
        render(<SpreadCard dto={makeDto()} />, { wrapper: MemoryRouter });

        expect(screen.getByText('BTC/USDT')).toBeInTheDocument();
        expect(screen.getByText('Bybit / Binance')).toBeInTheDocument();
        expect(screen.getByText('2.50%')).toBeInTheDocument();
        expect(screen.getByRole('link')).toHaveAttribute('href', '/en/spread?id=spread-1');
    });
});
