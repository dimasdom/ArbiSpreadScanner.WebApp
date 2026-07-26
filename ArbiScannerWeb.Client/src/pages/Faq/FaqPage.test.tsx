import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import FaqPage from './FaqPage';
import { createLocalStorageMock } from '../../test/localStorageMock';

describe('FaqPage', () => {
    let storage: Storage;

    beforeEach(() => {
        storage = createLocalStorageMock();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(storage);
    });

    it('renders the header and all FAQ questions', () => {
        render(<FaqPage />);

        expect(screen.getByText('header.title')).toBeInTheDocument();
        expect(screen.getByText('items.whatIs.question')).toBeInTheDocument();
        expect(screen.getByText('items.fundingRate.question')).toBeInTheDocument();
    });

    it('expands a FAQ item when clicked', async () => {
        render(<FaqPage />);
        const question = screen.getByText('items.whatIs.question');

        await userEvent.click(question.closest('button')!);

        const answerContainer = screen.getByText('items.whatIs.answer').parentElement;
        expect(answerContainer).toHaveClass('max-h-[2000px]');
    });

    it('renders the contact support link', () => {
        render(<FaqPage />);

        expect(screen.getByRole('link', { name: 'contact.button' })).toHaveAttribute(
            'href',
            'mailto:arbiscannerweb@atomicmail.io',
        );
    });

    it('clears stored guide-seen flags and reloads when "see guides again" is clicked', async () => {
        storage.setItem('guides-all-disabled', 'true');
        storage.setItem('guide_main_seen', 'true');
        storage.setItem('unrelated_key', 'some-other-value');
        const reloadMock = vi.fn();
        vi.stubGlobal('location', { ...globalThis.location, reload: reloadMock });
        render(<FaqPage />);

        await userEvent.click(screen.getByRole('button', { name: /guides.seeAgain/ }));

        expect(storage.getItem('guides-all-disabled')).toBeNull();
        expect(storage.getItem('guide_main_seen')).toBeNull();
        expect(storage.getItem('unrelated_key')).toBe('some-other-value');
        expect(reloadMock).toHaveBeenCalledOnce();
    });
});
