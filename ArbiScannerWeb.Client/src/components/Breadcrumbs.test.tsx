import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import Breadcrumbs from './Breadcrumbs';

function renderAt(path: string) {
    return render(
        <MemoryRouter initialEntries={[path]}>
            <Breadcrumbs />
        </MemoryRouter>,
    );
}

describe('Breadcrumbs', () => {
    it('renders nothing on the home page', () => {
        const { container } = renderAt('/en');

        expect(container).toBeEmptyDOMElement();
    });

    it('renders nothing for unknown routes', () => {
        const { container } = renderAt('/en/not-a-real-page');

        expect(container).toBeEmptyDOMElement();
    });

    it('shows a single crumb for a top-level page', () => {
        renderAt('/en/faq');

        expect(screen.getByText('nav.main')).toBeInTheDocument();
        expect(screen.getByText('nav.faq')).toBeInTheDocument();
    });

    it('marks the current page as the active crumb', () => {
        renderAt('/en/account');

        expect(screen.getByText('nav.account')).toHaveAttribute('aria-current', 'page');
    });

    it('builds the full ancestor chain for a nested route', () => {
        renderAt('/en/payment/pay');

        expect(screen.getByText('nav.main')).toBeInTheDocument();
        expect(screen.getByText('nav.subscriptions')).toBeInTheDocument();
        expect(screen.getByText('breadcrumbs.payment')).toBeInTheDocument();
        expect(screen.getByText('breadcrumbs.checkout')).toHaveAttribute('aria-current', 'page');
    });

    it('links ancestor crumbs but not the active one', () => {
        renderAt('/en/spread');

        expect(screen.getByText('nav.spreads').closest('a')).toHaveAttribute('href', '/en/spreads');
        expect(screen.getByText('breadcrumbs.spreadDetails').closest('a')).toBeNull();
    });

    it('strips the language prefix when matching routes', () => {
        renderAt('/uk/spreads');

        expect(screen.getByText('nav.spreads')).toBeInTheDocument();
    });
});
