import { useTranslation } from 'react-i18next';
import SearchOffIcon from '@mui/icons-material/SearchOff';
import Link from '../../components/LocalizedLink';

function NotFoundPage() {
    const { t } = useTranslation('common');

    return (
        <div className="max-w-3xl mx-auto mt-12 px-4 sm:px-6 lg:px-8 pb-20">
            <div className="bg-white dark:bg-gray-900 rounded-3xl shadow-xl overflow-hidden border border-gray-100 dark:border-gray-700 text-center transition-colors duration-200">
                <div className="bg-indigo-50 dark:bg-indigo-900/20 p-10 sm:p-12 border-b border-indigo-100 dark:border-indigo-800">
                    <div className="w-24 h-24 bg-indigo-100 dark:bg-indigo-900/40 rounded-full flex items-center justify-center mx-auto mb-6 text-indigo-600 dark:text-indigo-400 shadow-sm">
                        <SearchOffIcon style={{ fontSize: 56 }} />
                    </div>
                    <p className="text-6xl sm:text-7xl font-extrabold text-indigo-600 dark:text-indigo-400 tracking-tight">
                        404
                    </p>
                    <h1 className="mt-4 text-2xl sm:text-3xl font-extrabold text-gray-900 dark:text-gray-100 tracking-tight">
                        {t('notFound.title')}
                    </h1>
                </div>

                <div className="p-8 sm:p-12 space-y-8">
                    <p className="text-gray-600 dark:text-gray-400 text-base sm:text-lg max-w-xl mx-auto leading-relaxed">
                        {t('notFound.description')}
                    </p>

                    <Link
                        to="/"
                        className="inline-block w-full md:w-auto px-10 py-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg hover:shadow-indigo-200 transform hover:-translate-y-0.5 transition-all duration-200 text-lg"
                    >
                        {t('notFound.backHome')}
                    </Link>
                </div>
            </div>
        </div>
    );
}

export default NotFoundPage;
