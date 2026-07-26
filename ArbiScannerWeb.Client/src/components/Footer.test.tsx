import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import Footer from './Footer';

describe('Footer', () => {
    it('renders a localized link to the FAQ page', () => {
        render(<Footer />, { wrapper: MemoryRouter });

        expect(screen.getByRole('link', { name: 'footer.faq' })).toHaveAttribute('href', '/en/faq');
    });
});
