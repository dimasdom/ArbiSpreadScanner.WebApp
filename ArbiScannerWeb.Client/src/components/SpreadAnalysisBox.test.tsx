import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import SpreadAnalysisBox from './SpreadAnalysisBox';
import { createEmptyPossiblePositionModel } from '../types/tradeOpportunityModel';
import type { TradeOpportunityDetailsDTO } from '../types/tradeOpportunityModel';

function makeDto(overrides: Partial<TradeOpportunityDetailsDTO> = {}): TradeOpportunityDetailsDTO {
    return {
        positionModel: { ...createEmptyPossiblePositionModel(), guid: 'spread-1', symbol: 'BTC/USDT', spread: 2 },
        tickers: [],
        groupName: 'group',
        analysis: null,
        ...overrides,
    };
}

describe('SpreadAnalysisBox', () => {
    it('shows the recommended pill and reasons when recommended is true', () => {
        render(
            <SpreadAnalysisBox spread={makeDto({ analysis: { recommended: true, reasons: ['Spread is above 1%'] } })} />,
            { wrapper: MemoryRouter },
        );

        expect(screen.getByText('widget.recommended')).toBeInTheDocument();
        expect(screen.queryByText('widget.notRecommended')).not.toBeInTheDocument();
        expect(screen.getByText('Spread is above 1%')).toBeInTheDocument();
    });

    it('shows the not-recommended pill when recommended is false', () => {
        render(
            <SpreadAnalysisBox spread={makeDto({ analysis: { recommended: false, reasons: ['Cost exceeds threshold'] } })} />,
            { wrapper: MemoryRouter },
        );

        expect(screen.getByText('widget.notRecommended')).toBeInTheDocument();
    });

    it('renders the trend warning only when present', () => {
        render(
            <SpreadAnalysisBox
                spread={makeDto({ analysis: { recommended: true, reasons: [], trendWarning: 'Falling fast' } })}
            />,
            { wrapper: MemoryRouter },
        );

        expect(screen.getByText('Falling fast')).toBeInTheDocument();
    });

    it('renders no verdict/reasons when analysis is null', () => {
        render(<SpreadAnalysisBox spread={makeDto({ analysis: null })} />, { wrapper: MemoryRouter });

        expect(screen.queryByText('widget.recommended')).not.toBeInTheDocument();
        expect(screen.queryByText('widget.notRecommended')).not.toBeInTheDocument();
    });
});
