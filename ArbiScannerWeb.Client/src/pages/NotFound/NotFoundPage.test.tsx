import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import NotFoundPage from './NotFoundPage';

describe('NotFoundPage', () => {
    it('renders the not-found message and a link back to the localized home page', () => {
        render(<NotFoundPage />, { wrapper: MemoryRouter });

        expect(screen.getByText('notFound.title')).toBeInTheDocument();
        expect(screen.getByText('notFound.description')).toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'notFound.backHome' })).toHaveAttribute('href', '/en/');
    });
});
