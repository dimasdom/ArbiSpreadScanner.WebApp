import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { SnapshotIndexEntry, SpreadStatsSnapshot } from '../../types/StatsType';
import { SpreadType } from '../../types/SpreadType';
import StatsPage from './StatsPage';

const useStatsPageMock = vi.fn();

vi.mock('./hooks/useStatsPage', () => ({ useStatsPage: () => useStatsPageMock() }));
vi.mock('./components/BarStatChart', () => ({
    default: (props: { categories: string[]; valueSuffix?: string }) => (
        <div data-testid="bar-chart">{`${props.categories.join(',')}${props.valueSuffix ?? ''}`}</div>
    ),
}));
vi.mock('./components/DonutStatChart', () => ({
    default: (props: { labels: string[] }) => <div data-testid="donut-chart">{props.labels.join(',')}</div>,
}));

const snapshot: SpreadStatsSnapshot = {
    id: 'snap-1',
    generatedAtUtc: '2026-01-03T12:34:00Z',
    totalSpreadsAnalyzed: 42,
    topSymbolsByAverageSpread: [{ symbol: 'BTC/USDT', averageSpreadPercent: 1.5, sampleCount: 3 }],
    topExchangesByCount: [{ exchange: 'Binance', count: 10 }],
    topExchangePairsByCount: [{ exchangeA: 'Binance', exchangeB: 'Bybit', count: 5 }],
    medianVolumeByExchange: [{ exchange: 'Binance', medianVolume: 1000, sampleCount: 3 }],
    spreadTypeDistribution: [
        { type: SpreadType.Futures, count: 20 },
        { type: SpreadType.Spot, count: 22 },
    ],
    topSymbolsByCount: [{ symbol: 'ETH/USDT', count: 7 }],
};

const snapshots: SnapshotIndexEntry[] = [
    { id: 'snap-1', generatedAtUtc: '2026-01-03T12:34:00Z' },
    { id: 'snap-0', generatedAtUtc: '2026-01-03T08:00:00Z' },
];

// Mirrors StatsPage's own DDMMYYYY formatter, computed at test time so the
// assertion doesn't depend on which timezone the test runner happens to use.
function expectedOptionLabel(generatedAtUtc: string): string {
    const date = new Date(generatedAtUtc);
    const dd = String(date.getDate()).padStart(2, '0');
    const mm = String(date.getMonth() + 1).padStart(2, '0');
    const yyyy = date.getFullYear();
    return `${dd}${mm}${yyyy}`;
}

function baseHookValue(overrides: Partial<ReturnType<typeof useStatsPageMock>> = {}) {
    return {
        snapshot: undefined,
        isLoading: false,
        isError: false,
        hasAnySnapshot: false,
        snapshots: [],
        selectedSnapshotId: '',
        isViewingLatest: true,
        handleSnapshotChange: vi.fn(),
        ...overrides,
    };
}

describe('StatsPage', () => {
    beforeEach(() => {
        useStatsPageMock.mockReturnValue(baseHookValue());
    });

    it('shows a loading spinner while data is loading', () => {
        useStatsPageMock.mockReturnValue(baseHookValue({ isLoading: true }));

        const { container } = render(<StatsPage />);

        expect(container.querySelector('.animate-spin')).not.toBeNull();
    });

    it('shows the empty state when no snapshot has ever been generated', () => {
        useStatsPageMock.mockReturnValue(baseHookValue({ hasAnySnapshot: false, snapshot: undefined }));

        render(<StatsPage />);

        expect(screen.getByText('empty.noSnapshot')).toBeInTheDocument();
    });

    it('shows the empty state when the query failed', () => {
        useStatsPageMock.mockReturnValue(baseHookValue({ isError: true }));

        render(<StatsPage />);

        expect(screen.getByText('empty.noSnapshot')).toBeInTheDocument();
    });

    it('does not show the snapshot selector when there is nothing to select from', () => {
        useStatsPageMock.mockReturnValue(baseHookValue({ hasAnySnapshot: false }));

        render(<StatsPage />);

        expect(screen.queryByLabelText('dateSelector.label')).not.toBeInTheDocument();
    });

    it('renders the snapshot summary and chart panels once loaded', () => {
        useStatsPageMock.mockReturnValue(baseHookValue({
            snapshot,
            hasAnySnapshot: true,
            snapshots,
            selectedSnapshotId: snapshot.id,
        }));

        render(<StatsPage />);

        expect(screen.getByText('page.title')).toBeInTheDocument();
        expect(screen.getByText('page.totalAnalyzed')).toBeInTheDocument();
        expect(screen.getByText('page.generatedAt')).toBeInTheDocument();

        // The carousel renders every panel's content; check each mapped chart got the right data.
        const barCharts = screen.getAllByTestId('bar-chart');
        expect(barCharts[0]).toHaveTextContent('BTC/USDT%');
        expect(barCharts[1]).toHaveTextContent('Binance');
        expect(barCharts[2]).toHaveTextContent('Binance ↔ Bybit');
        expect(barCharts[3]).toHaveTextContent('Binance');
        expect(barCharts[4]).toHaveTextContent('ETH/USDT');
        expect(screen.getByTestId('donut-chart')).toHaveTextContent('Futures,Spot');
    });

    it('populates the select with one option per available snapshot, formatted as DDMMYYYY', () => {
        useStatsPageMock.mockReturnValue(baseHookValue({
            snapshot,
            hasAnySnapshot: true,
            snapshots,
            selectedSnapshotId: 'snap-1',
        }));

        render(<StatsPage />);

        const select = screen.getByLabelText('dateSelector.label') as HTMLSelectElement;
        expect(select.value).toBe('snap-1');

        const options = screen.getAllByRole('option') as HTMLOptionElement[];
        expect(options).toHaveLength(2);
        expect(options[0].value).toBe('snap-1');
        expect(options[0].textContent).toBe(expectedOptionLabel(snapshots[0].generatedAtUtc));
        expect(options[1].value).toBe('snap-0');
        expect(options[1].textContent).toBe(expectedOptionLabel(snapshots[1].generatedAtUtc));
    });

    it('forwards the chosen snapshot id when the select changes', () => {
        const handleSnapshotChange = vi.fn();
        useStatsPageMock.mockReturnValue(baseHookValue({
            snapshot,
            hasAnySnapshot: true,
            snapshots,
            selectedSnapshotId: 'snap-1',
            handleSnapshotChange,
        }));

        render(<StatsPage />);
        const select = screen.getByLabelText('dateSelector.label');

        fireEvent.change(select, { target: { value: 'snap-0' } });

        expect(handleSnapshotChange).toHaveBeenCalledWith('snap-0');
    });

    it('hides the "latest" reset button while already viewing the latest snapshot', () => {
        useStatsPageMock.mockReturnValue(baseHookValue({ snapshot, hasAnySnapshot: true, snapshots, isViewingLatest: true }));

        render(<StatsPage />);

        expect(screen.queryByText('dateSelector.latest')).not.toBeInTheDocument();
    });

    it('resets to the latest snapshot when the "latest" button is clicked', async () => {
        const handleSnapshotChange = vi.fn();
        useStatsPageMock.mockReturnValue(baseHookValue({
            snapshot, hasAnySnapshot: true, snapshots, isViewingLatest: false, handleSnapshotChange,
        }));

        render(<StatsPage />);
        await userEvent.click(screen.getByText('dateSelector.latest'));

        expect(handleSnapshotChange).toHaveBeenCalledWith('');
    });
});
