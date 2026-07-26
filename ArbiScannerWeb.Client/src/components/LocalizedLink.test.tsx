import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import LocalizedLink from './LocalizedLink';

describe('LocalizedLink', () => {
    it('prepends the current language to a string "to" prop', () => {
        render(<LocalizedLink to="/faq">FAQ</LocalizedLink>, { wrapper: MemoryRouter });

        expect(screen.getByRole('link', { name: 'FAQ' })).toHaveAttribute('href', '/en/faq');
    });

    it('prepends the current language to an object "to" prop', () => {
        render(<LocalizedLink to={{ pathname: '/faq', search: '?x=1' }}>FAQ</LocalizedLink>, { wrapper: MemoryRouter });

        expect(screen.getByRole('link', { name: 'FAQ' })).toHaveAttribute('href', '/en/faq?x=1');
    });

    it('leaves an object "to" prop without a pathname unchanged', () => {
        render(<LocalizedLink to={{ hash: '#section' }}>Jump</LocalizedLink>, { wrapper: MemoryRouter });

        expect(screen.getByRole('link', { name: 'Jump' })).toHaveAttribute('href', '/#section');
    });
});
