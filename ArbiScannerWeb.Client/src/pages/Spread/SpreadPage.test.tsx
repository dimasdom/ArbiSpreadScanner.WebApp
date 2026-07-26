import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SpreadPage from './SpreadPage';
import { createLocalStorageMock } from '../../test/localStorageMock';

const useSpreadPageMock = vi.fn();

vi.mock('./hooks/useSpreadPage', () => ({ useSpreadPage: () => useSpreadPageMock() }));
vi.mock('react-apexcharts', () => ({ default: () => <div data-testid="apex-chart" /> }));
vi.mock('../../contexts/ThemeContext', () => ({ useTheme: () => ({ theme: 'light' }) }));

function buildPositionModel(overrides: Partial<Record<string, unknown>> = {}) {
    return {
        guid: 'g1',
        symbol: 'BTC/USDT',
        startSpread: 1.2,
        summaryTarrif: 0.01,
        possibleProfit: 12.3,
        exchangeShort: { exchange: 'binance', fundingRateValue: 0.001 },
        exchangeLong: { exchange: 'okx', fundingRateValue: null },
        exchangeRateA: { exchange: 'binance' },
        exchangeRateB: { exchange: 'okx' },
        ...overrides,
    };
}

function baseHookValue(overrides: Partial<ReturnType<typeof useSpreadPageMock>> = {}) {
    return {
        possiblePositionDTO: { positionModel: buildPositionModel(), shortExchangeUrl: null, longExchangeUrl: null },
        tickers: [],
        isLoading: false,
        isSpotSpread: false,
        displayedShortRate: 100,
        displayedLongRate: 101,
        spreadVal: 1.5,
        isSpreadClosedDialogOpen: false,
        displayedVolatility: 0.5,
        pos: buildPositionModel(),
        asksA: undefined,
        bidsA: undefined,
        asksB: undefined,
        bidsB: undefined,
        totalSlippage: null,
        positionSize: 0,
        spreadLabel: 'Futures',
        spreadClass: 'bg-indigo-100 text-indigo-700',
        handleSpreadClosedConfirm: vi.fn(),
        ...overrides,
    };
}

describe('SpreadPage', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
        useSpreadPageMock.mockReturnValue(baseHookValue());
    });

    it('shows a loading spinner while data is loading', () => {
        useSpreadPageMock.mockReturnValue(baseHookValue({ isLoading: true }));

        const { container } = render(<SpreadPage />);

        expect(container.querySelector('.animate-spin')).not.toBeNull();
    });

    it('renders the spread details once loaded', () => {
        render(<SpreadPage />);

        expect(screen.getAllByText('BTC/USDT').length).toBeGreaterThan(0);
        expect(screen.getByText('binance', { exact: false })).toBeInTheDocument();
    });

    it('renders exchange plain text when no exchange URL is provided', () => {
        render(<SpreadPage />);

        expect(screen.queryByRole('link')).not.toBeInTheDocument();
    });

    it('renders exchange links when URLs are provided', () => {
        useSpreadPageMock.mockReturnValue(baseHookValue({
            possiblePositionDTO: {
                positionModel: buildPositionModel(),
                shortExchangeUrl: 'https://short.example',
                longExchangeUrl: 'https://long.example',
            },
        }));

        render(<SpreadPage />);

        const links = screen.getAllByRole('link');
        expect(links).toHaveLength(2);
        expect(links[0]).toHaveAttribute('href', 'https://short.example');
    });

    it('renders order books when order data is available', () => {
        useSpreadPageMock.mockReturnValue(baseHookValue({
            asksA: [{ price: 1, amount: 1 }],
            bidsA: [{ price: 1, amount: 1 }],
            asksB: [{ price: 1, amount: 1 }],
            bidsB: [{ price: 1, amount: 1 }],
        }));

        render(<SpreadPage />);

        expect(screen.getAllByText('orderBlock.headers.side')).toHaveLength(2);
    });

    it('does not render order books when order data is incomplete', () => {
        render(<SpreadPage />);

        expect(screen.queryByText('orderBlock.headers.side')).not.toBeInTheDocument();
    });

    it('shows the closed-spread dialog and confirms it', async () => {
        const handleSpreadClosedConfirm = vi.fn();
        useSpreadPageMock.mockReturnValue(baseHookValue({ isSpreadClosedDialogOpen: true, handleSpreadClosedConfirm }));

        render(<SpreadPage />);
        await userEvent.click(screen.getByRole('button', { name: 'spreadPage.closedDialog.ok' }));

        expect(handleSpreadClosedConfirm).toHaveBeenCalledOnce();
    });

    it('shows a slippage warning badge when total slippage is present', () => {
        useSpreadPageMock.mockReturnValue(baseHookValue({ totalSlippage: 0.5, positionSize: 100 }));

        render(<SpreadPage />);

        expect(screen.getByText(/spreadPage.details.estSlippage/)).toBeInTheDocument();
        expect(screen.getByText(/spreadPage.details.forPosition/)).toBeInTheDocument();
    });
});
