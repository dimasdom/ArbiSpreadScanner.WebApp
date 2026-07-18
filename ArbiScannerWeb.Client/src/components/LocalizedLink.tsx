import { Link, type LinkProps } from 'react-router';
import { useTranslation } from 'react-i18next';
import { withLang } from '../i18n/routing';

// Drop-in replacement for react-router's <Link> that keeps the current
// language segment on internal, absolute links. Imported as `Link` at
// call sites so no JSX needs to change.
export default function LocalizedLink({ to, ...rest }: LinkProps) {
    const { i18n } = useTranslation();
    const resolved =
        typeof to === 'string'
            ? withLang(to, i18n.language)
            : to.pathname
                ? { ...to, pathname: withLang(to.pathname, i18n.language) }
                : to;

    return <Link to={resolved} {...rest} />;
}
