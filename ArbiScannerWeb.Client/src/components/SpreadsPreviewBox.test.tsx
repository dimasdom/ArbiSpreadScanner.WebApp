import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import SpreadsPreviewBox from './SpreadsPreviewBox';
import { createEmptyPossiblePositionModel } from '../types/tradeOpportunityModel';
import type { TradeOpportunityDetailsDTO } from '../types/tradeOpportunityModel';

function makeDto(guid: string): TradeOpportunityDetailsDTO {
    return {
        positionModel: { ...createEmptyPossiblePositionModel(), guid, symbol: `SYM-${guid}`, spread: 1 },
        tickers: [],
        groupName: 'group',
        analysis: null,
    };
}

describe('SpreadsPreviewBox', () => {
    it('renders exactly the passed-in preview items', () => {
        render(<SpreadsPreviewBox spreads={[makeDto('a'), makeDto('b')]} totalCount={2} />, { wrapper: MemoryRouter });

        expect(screen.getByText('SYM-a')).toBeInTheDocument();
        expect(screen.getByText('SYM-b')).toBeInTheDocument();
    });

    it('shows the "view more" link when totalCount exceeds the shown items', () => {
        render(<SpreadsPreviewBox spreads={[makeDto('a')]} totalCount={5} />, { wrapper: MemoryRouter });

        expect(screen.getByText('widget.viewOnSpreadsPage')).toBeInTheDocument();
    });

    it('hides the "view more" link when every item is already shown', () => {
        render(<SpreadsPreviewBox spreads={[makeDto('a')]} totalCount={1} />, { wrapper: MemoryRouter });

        expect(screen.queryByText('widget.viewOnSpreadsPage')).not.toBeInTheDocument();
    });

    it('skips a malformed item (missing positionModel) instead of crashing', () => {
        const malformed = {} as TradeOpportunityDetailsDTO;

        expect(() =>
            render(<SpreadsPreviewBox spreads={[malformed, makeDto('b')]} totalCount={2} />, { wrapper: MemoryRouter }),
        ).not.toThrow();
        expect(screen.getByText('SYM-b')).toBeInTheDocument();
    });
});
