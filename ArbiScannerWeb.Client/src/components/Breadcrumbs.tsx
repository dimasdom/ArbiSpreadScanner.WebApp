import { useLocation } from 'react-router';
import { useTranslation } from 'react-i18next';
import Link from './LocalizedLink';
import { stripLangPrefix } from '../i18n/routing';

interface RouteMeta {
    labelKey: string;
    parent?: string;
}

// Keyed by the language-stripped, slash-trimmed pathname. `parent` chains a
// route up to its logical section (e.g. a single spread belongs under
// "spreads") even though the URL itself isn't nested.
const routeMeta: Record<string, RouteMeta> = {
    account: { labelKey: 'nav.account' },
    'mcp-token': { labelKey: 'nav.mcpToken' },
    spreads: { labelKey: 'nav.spreads' },
    spread: { labelKey: 'breadcrumbs.spreadDetails', parent: 'spreads' },
    subscriptions: { labelKey: 'nav.subscriptions' },
    payment: { labelKey: 'breadcrumbs.payment', parent: 'subscriptions' },
    'payment/pay': { labelKey: 'breadcrumbs.checkout', parent: 'payment' },
    faq: { labelKey: 'nav.faq' },
};

function buildTrail(path: string): string[] {
    const trail: string[] = [];
    let current: string | undefined = path;
    while (current) {
        trail.unshift(current);
        current = routeMeta[current]?.parent;
    }
    return trail;
}

export default function Breadcrumbs() {
    const { t } = useTranslation('common');
    const location = useLocation();
    const path = stripLangPrefix(location.pathname).replace(/^\/+|\/+$/g, '');

    if (!routeMeta[path]) return null;

    const trail = buildTrail(path);

    return (
        <nav aria-label={t('breadcrumbs.aria')} className="max-w-5xl mx-auto px-5 pt-4">
            <ol className="flex flex-wrap items-center gap-1 text-sm text-gray-500 dark:text-gray-400">
                <li>
                    <Link to="/" className="hover:text-indigo-600 dark:hover:text-indigo-400 transition-colors">
                        {t('nav.main')}
                    </Link>
                </li>
                {trail.map((segment) => (
                    <li key={segment} className="flex items-center gap-1">
                        <span aria-hidden="true">/</span>
                        {segment === path ? (
                            <span className="text-gray-700 dark:text-gray-200 font-medium" aria-current="page">
                                {t(routeMeta[segment].labelKey)}
                            </span>
                        ) : (
                            <Link to={`/${segment}`} className="hover:text-indigo-600 dark:hover:text-indigo-400 transition-colors">
                                {t(routeMeta[segment].labelKey)}
                            </Link>
                        )}
                    </li>
                ))}
            </ol>
        </nav>
    );
}
