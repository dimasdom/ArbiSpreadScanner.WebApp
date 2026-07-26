import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import EulaModal from './EulaModal';

describe('EulaModal', () => {
    it('renders nothing when closed', () => {
        const { container } = render(<EulaModal isOpen={false} onAgree={vi.fn()} onCancel={vi.fn()} />);

        expect(container).toBeEmptyDOMElement();
    });

    it('renders the EULA content when open', () => {
        render(<EulaModal isOpen onAgree={vi.fn()} onCancel={vi.fn()} />);

        expect(screen.getByText('eula.title')).toBeInTheDocument();
    });

    it('invokes onAgree when the agree button is clicked', async () => {
        const onAgree = vi.fn();
        render(<EulaModal isOpen onAgree={onAgree} onCancel={vi.fn()} />);

        await userEvent.click(screen.getByRole('button', { name: 'eula.agree' }));

        expect(onAgree).toHaveBeenCalledOnce();
    });

    it('invokes onCancel when the cancel button is clicked', async () => {
        const onCancel = vi.fn();
        render(<EulaModal isOpen onAgree={vi.fn()} onCancel={onCancel} />);

        await userEvent.click(screen.getByRole('button', { name: 'actions.cancel' }));

        expect(onCancel).toHaveBeenCalledOnce();
    });
});
