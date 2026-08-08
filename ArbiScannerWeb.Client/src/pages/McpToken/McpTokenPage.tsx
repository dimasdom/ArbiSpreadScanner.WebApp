import React from 'react';
import toast from 'react-hot-toast';
import { useTranslation } from 'react-i18next';
import KeyIcon from '@mui/icons-material/Key';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import CheckIcon from '@mui/icons-material/Check';
import { useGenerateMcpTokenMutation } from '../../store/services/mcpToken';

export function McpTokenPage() {
    const { t } = useTranslation('mcpToken');
    const [generateMcpToken, { isLoading }] = useGenerateMcpTokenMutation();
    const [token, setToken] = React.useState('');
    const [copied, setCopied] = React.useState(false);

    const mcpServerUrl = `${window.location.origin}/mcp`;

    const handleGenerate = () => {
        setCopied(false);
        void generateMcpToken().unwrap()
            .then((result) => {
                if (result.isSuccess && result.value) {
                    setToken(result.value);
                    toast.success(t('toasts.generateSuccess'));
                } else {
                    toast.error(result.errors?.[0]?.message || t('toasts.generateError'));
                }
            })
            .catch(() => toast.error(t('toasts.generateError')));
    };

    const handleCopy = async () => {
        try {
            await navigator.clipboard.writeText(token);
            setCopied(true);
            toast.success(t('toasts.copySuccess'));
            setTimeout(() => setCopied(false), 2000);
        } catch {
            toast.error(t('toasts.copyError'));
        }
    };

    return (
        <div className="max-w-7xl mx-auto mt-8 px-4 sm:px-6 lg:px-8 pb-20">
            <div className="text-center mb-16 space-y-4">
                <h1 className="text-4xl md:text-5xl font-extrabold text-gray-900 dark:text-gray-100 tracking-tight">
                    {t('header.title')}
                </h1>
                <p className="text-lg md:text-xl text-gray-500 dark:text-gray-400 max-w-2xl mx-auto">
                    {t('header.subtitle')}
                </p>
            </div>

            <div className="bg-white dark:bg-gray-900 rounded-3xl shadow-xl overflow-hidden border border-gray-100 dark:border-gray-700 transition-colors duration-200">
                <div className="bg-gray-50 dark:bg-gray-800 p-6 md:p-8 border-b border-gray-100 dark:border-gray-700 flex items-center justify-center">
                    <div className="w-16 h-16 bg-white dark:bg-gray-700 rounded-full flex items-center justify-center shadow-sm text-indigo-600 dark:text-indigo-400">
                        <KeyIcon fontSize="large" />
                    </div>
                </div>

                <div className="p-8 md:p-12 space-y-12">
                    {/* Token generation */}
                    <div>
                        <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                            {t('token.label')}
                        </label>
                        <div className="flex flex-col sm:flex-row gap-3">
                            <input
                                type="text"
                                value={token}
                                readOnly
                                placeholder={t('token.placeholder')}
                                className="block w-full rounded-xl border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800 px-4 py-3 text-gray-900 dark:text-gray-100 shadow-sm font-mono text-sm"
                            />
                            {token && (
                                <button
                                    type="button"
                                    onClick={() => void handleCopy()}
                                    className="inline-flex items-center justify-center gap-2 px-6 py-3 bg-gray-100 dark:bg-gray-700 text-gray-900 dark:text-gray-100 rounded-xl font-semibold hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors shadow-sm shrink-0"
                                >
                                    {copied ? <CheckIcon fontSize="small" /> : <ContentCopyIcon fontSize="small" />}
                                    {copied ? t('token.copiedButton') : t('token.copyButton')}
                                </button>
                            )}
                        </div>

                        <button
                            type="button"
                            onClick={handleGenerate}
                            disabled={isLoading}
                            className="mt-4 inline-flex items-center px-6 py-3 bg-indigo-600 text-white rounded-xl font-bold shadow-lg transition-all duration-200 transform hover:-translate-y-0.5 hover:shadow-xl disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none"
                        >
                            {isLoading
                                ? t('generate.generating')
                                : token
                                    ? t('generate.regenerateButton')
                                    : t('generate.button')}
                        </button>

                        {token && (
                            <div className="mt-6 bg-amber-50 dark:bg-amber-900/20 rounded-2xl p-6 border border-amber-200 dark:border-amber-800">
                                <p className="text-sm text-amber-800 dark:text-amber-300">
                                    {t('token.warning')}
                                </p>
                            </div>
                        )}
                    </div>

                    <div className="border-t border-gray-100 dark:border-gray-700" />

                    {/* Instructions */}
                    <div>
                        <h3 className="text-lg font-bold text-gray-900 dark:text-gray-100 mb-6">
                            {t('instructions.title')}
                        </h3>
                        <ol className="space-y-4 text-gray-700 dark:text-gray-300">
                            <li className="flex gap-3">
                                <span className="shrink-0 w-6 h-6 rounded-full bg-indigo-100 dark:bg-indigo-900/40 text-indigo-700 dark:text-indigo-300 text-sm font-bold flex items-center justify-center">1</span>
                                <span>{t('instructions.step1')}</span>
                            </li>
                            <li className="flex gap-3">
                                <span className="shrink-0 w-6 h-6 rounded-full bg-indigo-100 dark:bg-indigo-900/40 text-indigo-700 dark:text-indigo-300 text-sm font-bold flex items-center justify-center">2</span>
                                <span>
                                    {t('instructions.step2')}{' '}
                                    <code className="px-2 py-1 rounded-lg bg-gray-100 dark:bg-gray-800 text-sm font-mono">{mcpServerUrl}</code>
                                </span>
                            </li>
                            <li className="flex gap-3">
                                <span className="shrink-0 w-6 h-6 rounded-full bg-indigo-100 dark:bg-indigo-900/40 text-indigo-700 dark:text-indigo-300 text-sm font-bold flex items-center justify-center">3</span>
                                <span>
                                    {t('instructions.step3')}{' '}
                                    <code className="px-2 py-1 rounded-lg bg-gray-100 dark:bg-gray-800 text-sm font-mono">Bearer &lt;{t('token.label')}&gt;</code>
                                </span>
                            </li>
                            <li className="flex gap-3">
                                <span className="shrink-0 w-6 h-6 rounded-full bg-indigo-100 dark:bg-indigo-900/40 text-indigo-700 dark:text-indigo-300 text-sm font-bold flex items-center justify-center">4</span>
                                <span>{t('instructions.step4')}</span>
                            </li>
                        </ol>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default McpTokenPage;
