import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import FaqElement from './FaqElement';

describe('FaqElement', () => {
    it('renders the question and hides the answer initially', () => {
        render(<FaqElement question="What is this?" answer="An answer" />);

        expect(screen.getByText('What is this?')).toBeInTheDocument();
        const answerContainer = screen.getByText('An answer').parentElement;
        expect(answerContainer).toHaveClass('max-h-0');
    });

    it('reveals the answer when clicked, and hides it again on a second click', async () => {
        render(<FaqElement question="What is this?" answer="An answer" />);
        const button = screen.getByRole('button');
        const answerContainer = screen.getByText('An answer').parentElement;

        await userEvent.click(button);
        expect(answerContainer).toHaveClass('max-h-[2000px]');

        await userEvent.click(button);
        expect(answerContainer).toHaveClass('max-h-0');
    });
});
