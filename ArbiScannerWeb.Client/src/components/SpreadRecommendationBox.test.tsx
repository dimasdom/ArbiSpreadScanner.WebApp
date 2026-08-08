import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import SpreadRecommendationBox from './SpreadRecommendationBox';
import { createEmptyPossiblePositionModel } from '../types/tradeOpportunityModel';
import type { RecommendedSpreadDTO } from '../types/tradeOpportunityModel';
import { SpreadType } from '../types/SpreadType';

function makeRecommendation(guid: string, category: SpreadType): RecommendedSpreadDTO {
    return {
        category,
        details: {
            positionModel: { ...createEmptyPossiblePositionModel(), guid, symbol: `SYM-${guid}`, spread: 1.5 },
            tickers: [],
            groupName: 'group',
            analysis: null,
        },
    };
}

describe('SpreadRecommendationBox', () => {
    it('shows the empty-state fallback when there are no recommendations', () => {
        render(<SpreadRecommendationBox spreads={[]} />, { wrapper: MemoryRouter });

        expect(screen.getByText('widget.noRecommendations')).toBeInTheDocument();
    });

    it('renders a category heading and a card only for categories with recommendations', () => {
        render(
            <SpreadRecommendationBox
                spreads={[
                    makeRecommendation('a', SpreadType.Futures),
                    makeRecommendation('b', SpreadType.Futures),
                ]}
            />,
            { wrapper: MemoryRouter },
        );

        expect(screen.getByText('SYM-a')).toBeInTheDocument();
        expect(screen.getByText('SYM-b')).toBeInTheDocument();
        expect(screen.queryByText('SYM-c')).not.toBeInTheDocument();
        expect(screen.getAllByRole('link')).toHaveLength(2);
    });

    it('skips a malformed item (missing details) instead of crashing', () => {
        const malformed = { category: SpreadType.Futures, details: undefined } as unknown as RecommendedSpreadDTO;

        expect(() =>
            render(
                <SpreadRecommendationBox spreads={[malformed, makeRecommendation('b', SpreadType.Futures)]} />,
                { wrapper: MemoryRouter },
            ),
        ).not.toThrow();
        expect(screen.getByText('SYM-b')).toBeInTheDocument();
    });
});
