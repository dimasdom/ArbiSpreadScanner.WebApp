import React, { useEffect, useState } from 'react';
import { Link } from "react-router";

interface HeaderProps {
    isLoggedIn: boolean;
    isActiveSubscription: boolean;
    onLogin: () => void;
    onLogout: () => void;
}

const navLinksDefault = [
    { label: 'Main', to: '/' },
    { label: 'Subscriptions', to: '/subscriptions' },
    { label: 'FAQ', to: '/faq' }
];
const navLinksLoggedInNotActive = [
    { label: 'Main', to: '/' },
    { label: 'Account', to: '/account' },
    { label: 'Subscriptions', to: '/subscriptions' },
    { label: 'FAQ', to: '/faq' }
];
const navLinksLoggedInActive = [
    { label: 'Main', to: '/' },
    { label: 'Spreads', to: '/spreads' },
    { label: 'Account', to: '/account' },
    { label: 'FAQ', to: '/faq' }
];
const Header: React.FC<HeaderProps> = ({ isLoggedIn, isActiveSubscription, onLogin, onLogout }) => {
    const [sideMenuOpen, setSideMenuOpen] = useState(false);
    const [navLinks,setNavLinks] = useState(navLinksDefault);
    useEffect(() => {
        if (isLoggedIn) {
            if(isActiveSubscription){
                setNavLinks(navLinksLoggedInActive);
            }
            else{
                setNavLinks(navLinksLoggedInNotActive);
            }
        } else {
            setNavLinks(navLinksDefault);
        }
    }, [isLoggedIn,isActiveSubscription]);
    const toggleSideMenu = () => {
        setSideMenuOpen(!sideMenuOpen);
    };

    const closeSideMenu = () => {
        setSideMenuOpen(false);
    };

    // Hamburger icon component
    const HamburgerIcon = () => (
        <svg
            className="w-6 h-6"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            viewBox="0 0 24 24"
        >
            <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
        </svg>
    );

    // Close icon component
    const CloseIcon = () => (
        <svg
            className="w-6 h-6"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            viewBox="0 0 24 24"
        >
            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
    );

    return (
        <header className="fixed top-0 left-0 right-0 z-30 w-full py-3 px-5">
            <nav className="mx-auto max-w-5xl shadow-md rounded-2xl">
                <div className="bg-white rounded-2xl shadow-inner p-3">
                    <div className="flex items-center justify-between gap-4">
                        {/* Desktop Navigation */}
                        <div className="hidden md:flex items-center gap-2">
                            {navLinks.map((link) => (
                                <Link
                                    key={link.to}
                                    className="mr-5 ml-5 transition-shadow hover:shadow-md px-5 py-2 rounded-md hover:bg-gray-50"
                                    to={{
                                        pathname: link.to
                                    }}
                                >
                                    {link.label}
                                </Link>
                            ))}
                        </div>

                        {/* Mobile Hamburger Button */}
                        <div className="md:hidden">
                            <button
                                onClick={toggleSideMenu}
                                className="p-2 rounded-md bg-white shadow-md transition-colors text-gray-700"
                                aria-label="Toggle menu"
                            >
                                <HamburgerIcon />
                            </button>
                        </div>

                        {/* Auth Buttons */}
                        <div className="flex items-center gap-2 ml-auto md:ml-0">
                            {isLoggedIn ? (
                                <button
                                    onClick={onLogout}
                                    className="rounded-md px-3 py-1 bg-white shadow-sm hover:shadow-md transition-shadow text-sm md:text-base"
                                >
                                    Log out
                                </button>
                            ) : (
                                <button
                                    onClick={onLogin}
                                    className="rounded-md px-3 py-1 bg-white shadow-sm hover:shadow-md transition-shadow text-sm md:text-base"
                                >
                                    Log in
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            </nav>

            {/* Mobile Side Menu Overlay */}
            {sideMenuOpen && (
                <div
                    className="fixed inset-0 bg-black bg-opacity-50 z-40 md:hidden"
                    onClick={closeSideMenu}
                    aria-hidden="true"
                />
            )}

            {/* Mobile Side Menu */}
            <div
                className={`fixed top-0 left-0 h-screen w-64 bg-white shadow-lg z-50 transform transition-transform duration-300 ease-in-out md:hidden ${sideMenuOpen ? 'translate-x-0' : '-translate-x-full'
                    }`}
            >
                {/* Close Button */}
                <div className="flex items-center justify-between p-4 border-b border-gray-200">
                    <h2 className="text-lg font-semibold">Menu</h2>
                    <button
                        onClick={closeSideMenu}
                        className="p-2 rounded-md bg-white shadow-md transition-colors text-gray-700"
                        aria-label="Close menu"
                    >
                        <CloseIcon />
                    </button>
                </div>

                {/* Side Menu Links */}
                <div className="flex flex-col">
                    {navLinks.map((link) => (
                        <Link
                            key={link.to}
                            to={{ pathname: link.to }}
                            onClick={closeSideMenu}
                            className="px-6 py-3 border-b border-gray-100 text-gray-700 hover:bg-blue-50 hover:text-blue-600 transition-colors"
                        >
                            {link.label}
                        </Link>
                    ))}
                </div>

                {/* Side Menu Auth Button */}
                <div className="absolute bottom-0 left-0 right-0 p-4 border-t border-gray-200 bg-gray-50">
                    {isLoggedIn ? (
                        <button
                            onClick={() => {
                                onLogout();
                                closeSideMenu();
                            }}
                            className="w-full rounded-md px-4 py-2 bg-white text-black shadow-md hover:shadown-lg transition-colors"
                        >
                            Log out
                        </button>
                    ) : (
                        <button
                            onClick={() => {
                                onLogin();
                                closeSideMenu();
                            }}
                            className="w-full rounded-md px-4 py-2 bg-white text-black shadow-md hover:shadown-lg transition-colors"
                        >
                            Log in
                        </button>
                    )}
                </div>
            </div>
        </header>
    );
};

export default Header;