import { useTranslation } from 'react-i18next';

interface SuccessProps {
    message: string;
    title?: string;
    className?: string;
}

export default function SuccessComponent({ message, title, className = '' }: Readonly<SuccessProps>) {
    const { t } = useTranslation('common');
    return (
        <div className={`w-full max-w-md ${className}`}>
            <div className="bg-white dark:bg-gray-900 rounded-2xl shadow-md ring-1 ring-gray-50 dark:ring-gray-700 ring-inset p-6">
                <div className="flex items-start gap-4">
                    <div className="flex items-center justify-center h-12 w-12 rounded-full bg-green-50 dark:bg-green-900/30 ring-1 ring-green-100 dark:ring-green-800">
                        <svg className="h-6 w-6 text-green-600 dark:text-green-400" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden>
                            <path d="M5 13l4 4L19 7" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                    </div>

                    <div className="flex-1">
                        <h3 className="text-lg font-semibold text-gray-800 dark:text-gray-100">{title ?? t('successState.defaultTitle')}</h3>
                        <p className="mt-2 text-sm text-green-700 dark:text-green-400 bg-green-50 dark:bg-green-900/20 rounded-md px-3 py-2 shadow-md ring-1 ring-green-100 dark:ring-green-800 ring-inset">{message}</p>
                    </div>
                </div>
            </div>
        </div>
    );
}
