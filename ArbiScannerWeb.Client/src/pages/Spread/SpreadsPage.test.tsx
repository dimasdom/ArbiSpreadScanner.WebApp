import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import SpreadsPage from './SpreadsPage';
import { createLocalStorageMock } from '../../test/localStorageMock';

const useSpreadsPageMock = vi.fn();

interface GridRow { id: string; symbol: string }

vi.mock('./hooks/useSpreadsPage', () => ({ useSpreadsPage: () => useSpreadsPageMock() }));
vi.mock('@mui/x-data-grid', () => ({
    DataGrid: (props: { rows: GridRow[] }) => (
        <div data-testid="data-grid">
            {props.rows.map((r) => <div key={r.id}>{r.symbol}</div>)}
        </div>
    ),
}));

function baseHookValue(overrides: Partial<ReturnType<typeof useSpreadsPageMock>> = {}) {
    return {
        rows: [],
        isLoading: false,
        isError: false,
        isMobile: false,
        handleRowDoubleClick: vi.fn(),
        handleRowClick: vi.fn(),
        ...overrides,
    };
}

describe('SpreadsPage', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
        useSpreadsPageMock.mockReturnValue(baseHookValue());
    });

    it('shows a loading spinner while data is loading', () => {
        useSpreadsPageMock.mockReturnValue(baseHookValue({ isLoading: true }));

        const { container } = render(<SpreadsPage />);

        expect(container.querySelector('.animate-spin')).not.toBeNull();
    });

    it('shows an error state when the query fails', () => {
        useSpreadsPageMock.mockReturnValue(baseHookValue({ isError: true }));

        render(<SpreadsPage />);

        expect(screen.getByText('spreadsPage.errorLoading')).toBeInTheDocument();
    });

    it('renders the data grid with rows once loaded', () => {
        useSpreadsPageMock.mockReturnValue(baseHookValue({
            rows: [{ id: 'g1', guid: 'g1', spread: '1.2345', type: 'spreadType.Futures', symbol: 'BTC/USDT', exchangeLong: 'okx', exchangeShort: 'binance', exchangeRateLong: '101.000000', exchangeRateShort: '100.000000', fundingRateLong: '-', fundingRateShort: '-' }],
        }));

        render(<SpreadsPage />);

        expect(screen.getByText('BTC/USDT')).toBeInTheDocument();
    });
});
