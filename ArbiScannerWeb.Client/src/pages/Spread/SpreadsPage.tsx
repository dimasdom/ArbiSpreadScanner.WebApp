import { DataGrid, type GridColDef } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import GuideModal from '../../components/GuideModal';
import type { GuideStep } from '../../components/GuideModal';
import ErrorState from '../../components/ErrorState';
import { useSpreadsPage } from './hooks/useSpreadsPage';

function getSpreadsGuideSteps(t: TFunction<'spreads'>): GuideStep[] {
    return [
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M3 14h18M3 6h18M3 18h18" />
                </svg>
            ),
            title: t('guide.spreadsPage.steps.table.title'),
            description: t('guide.spreadsPage.steps.table.description'),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                </svg>
            ),
            title: t('guide.spreadsPage.steps.columns.title'),
            description: (
                <span>
                    <strong>{t('guide.spreadsPage.steps.columns.spreadLabel')}</strong> — {t('guide.spreadsPage.steps.columns.spreadDescription')}<br />
                    <strong>{t('guide.spreadsPage.steps.columns.typeLabel')}</strong> — {t('guide.spreadsPage.steps.columns.typeDescription')}<br />
                    <strong>{t('guide.spreadsPage.steps.columns.symbolLabel')}</strong> — {t('guide.spreadsPage.steps.columns.symbolDescription')}<br />
                    <strong>{t('guide.spreadsPage.steps.columns.exchangeLabel')}</strong> — {t('guide.spreadsPage.steps.columns.exchangeDescription')}<br />
                    <strong>{t('guide.spreadsPage.steps.columns.rateLabel')}</strong> — {t('guide.spreadsPage.steps.columns.rateDescription')}
                </span>
            ),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 15l-2 5L9 9l11 4-5 2zm0 0l5 5" />
                </svg>
            ),
            title: t('guide.spreadsPage.steps.opening.title'),
            description: t('guide.spreadsPage.steps.opening.description'),
        },
    ];
}

function SpreadsPage() {
    const { rows, isLoading, isError, handleRowDoubleClick, handleRowClick } = useSpreadsPage();
    const { t } = useTranslation('spreads');

    const columns: GridColDef[] = [
        { field: 'spread', headerName: t('spreadsPage.columns.spread'), width: 110 },
        { field: 'type', headerName: t('spreadsPage.columns.type'), width: 130 },
        { field: 'symbol', headerName: t('spreadsPage.columns.symbol'), width: 150 },
        { field: 'exchangeLong', headerName: t('spreadsPage.columns.exchangeLong'), width: 150 },
        { field: 'exchangeShort', headerName: t('spreadsPage.columns.exchangeShort'), width: 150 },
        { field: 'exchangeRateLong', headerName: t('spreadsPage.columns.rateLong'), width: 110 },
        { field: 'exchangeRateShort', headerName: t('spreadsPage.columns.rateShort'), width: 110 },
        { field: 'fundingRateLong', headerName: t('spreadsPage.columns.fundingRateLong'), width: 140 },
        { field: 'fundingRateShort', headerName: t('spreadsPage.columns.fundingRateShort'), width: 140 },
    ];

    const renderContent = () => {
        if (isLoading) {
            return (
                <div className="flex items-center justify-center h-64">
                    <div className="w-10 h-10 border-4 border-gray-200 dark:border-gray-700 border-t-indigo-500 rounded-full animate-spin" />
                </div>
            );
        }

        if (isError) {
            return <ErrorState message={t('spreadsPage.errorLoading')} />;
        }

        return (
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
        );
    };

    return (
        <div className="max-w-7xl mx-auto mt-6 px-4 sm:px-6 lg:px-8 pb-12">
            <GuideModal storageKey="guide_spreads_seen" title={t('guide.spreadsPage.title')} steps={getSpreadsGuideSteps(t)} />
            <div className="shadow-inner rounded-4xl p-4 md:p-5 bg-white dark:bg-gray-900 relative transition-colors duration-200">
                {renderContent()}
            </div>
        </div>
    );
}

export default SpreadsPage;
