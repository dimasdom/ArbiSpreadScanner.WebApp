import { Link, type LinkProps } from 'react-router';
import { useTranslation } from 'react-i18next';
import { withLang } from '../i18n/routing';

function resolveTo(to: LinkProps['to'], lang: string): LinkProps['to'] {
    if (typeof to === 'string') {
        return withLang(to, lang);
    }
    if (to.pathname) {
        return { ...to, pathname: withLang(to.pathname, lang) };
    }
    return to;
}

// Drop-in replacement for react-router's <Link> that keeps the current
// language segment on internal, absolute links. Imported as `Link` at
// call sites so no JSX needs to change.
export default function LocalizedLink({ to, ...rest }: Readonly<LinkProps>) {
    const { i18n } = useTranslation();
    const resolved = resolveTo(to, i18n.language);

    return <Link to={resolved} {...rest} />;
}
