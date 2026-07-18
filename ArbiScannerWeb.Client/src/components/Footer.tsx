import { Trans, useTranslation } from 'react-i18next';
import Link from './LocalizedLink';

const Footer = () => {
    const { t } = useTranslation('common');
    return (
        <footer className="py-6 mt-auto">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                <div className="flex flex-col md:flex-row justify-between items-center space-y-4 md:space-y-0">
                    <div className="text-center md:text-left">
                        <p className="text-sm text-gray-600 dark:text-gray-400">
                            <Trans
                                i18nKey="footer.copyright"
                                ns="common"
                                values={{ year: new Date().getFullYear() }}
                                components={{ brand: <span className="font-semibold text-gray-800 dark:text-gray-200" /> }}
                            />
                        </p>
                    </div>
                    <div className="flex space-x-6">
                        <Link
                            to="/faq"
                            className="text-sm text-gray-600 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200 transition-colors duration-200"
                        >
                            {t('footer.faq')}
                        </Link>
                    </div>
                </div>
            </div>
        </footer>
    );
};

export default Footer;
