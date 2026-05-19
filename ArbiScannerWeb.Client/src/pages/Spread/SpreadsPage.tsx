import { DataGrid, type GridColDef } from '@mui/x-data-grid';
import GuideModal from '../../components/GuideModal';
import type { GuideStep } from '../../components/GuideModal';
import { useSpreadsPage } from './hooks/useSpreadsPage';

const spreadsGuideSteps: GuideStep[] = [
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M3 14h18M3 6h18M3 18h18" />
            </svg>
        ),
        title: 'Live Spreads Table',
        description: 'This table shows all currently active arbitrage opportunities detected across exchanges. Each row is a live spread — data refreshes automatically as market conditions change.',
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
        ),
        title: 'Understanding the Columns',
        description: (
            <span>
                <strong>Spread</strong> — the profit opportunity (%) after fees.<br />
                <strong>Type</strong> — Futures, Funding, or Spot spread.<br />
                <strong>Symbol</strong> — the trading pair (e.g. BTC/USDT).<br />
                <strong>Exchange Long/Short</strong> — where to place each leg.<br />
                <strong>Rate Long/Short</strong> — current prices on each side.
            </span>
        ),
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 15l-2 5L9 9l11 4-5 2zm0 0l5 5" />
            </svg>
        ),
        title: 'Opening a Spread',
        description: 'Double-click any row (or tap on mobile) to open the full spread detail page with a live chart, order book depth, funding rates, and volatility metrics.',
    },
];

function SpreadsPage() {
    const { rows, isLoading, handleRowDoubleClick, handleRowClick } = useSpreadsPage();

    const columns: GridColDef[] = [
        { field: 'spread', headerName: 'Spread', width: 110 },
        {
            field: 'type',
            headerName: 'Type',
            width: 130,
        },
        { field: 'symbol', headerName: 'Symbol', width: 150 },
        { field: 'exchangeLong', headerName: 'Exchange Long', width: 150 },
        { field: 'exchangeShort', headerName: 'Exchange Short', width: 150 },
        { field: 'exchangeRateLong', headerName: 'Rate Long', width: 110 },
        { field: 'exchangeRateShort', headerName: 'Rate Short', width: 110 },
        { field: 'fundingRateLong', headerName: 'Funding Rate Long', width: 140 },
        { field: 'fundingRateShort', headerName: 'Funding Rate Short', width: 140 },
    ];

    return (
        <div className="shadow-inner max-w-7xl mx-auto mt-6 rounded-4xl p-4 md:p-5 bg-white min-h-screen md:min-h-auto relative">
            <GuideModal storageKey="guide_spreads_seen" title="Using the Spreads Table" steps={spreadsGuideSteps} />
            {isLoading ? (
                <div className="flex items-center justify-center h-64">
                    <div className="w-10 h-10 border-4 border-gray-200 border-t-indigo-500 rounded-full animate-spin" />
                </div>
            ) : (
            <div className="rounded-4xl shadow-2xl w-full px-2 md:px-5 py-5">
                <DataGrid
                    rows={rows}
                    columns={columns}
                    pageSizeOptions={[10, 25, 50]}
                    initialState={{
                        pagination: { paginationModel: { pageSize: 10, page: 0 } },
                    }}
                    disableRowSelectionOnClick
                    onRowDoubleClick={handleRowDoubleClick}
                    onRowClick={handleRowClick}
                />
            </div>
            )}
        </div>

    );
}

export default SpreadsPage;