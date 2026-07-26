import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TelegramLinkModal from './TelegramLinkModal';

describe('TelegramLinkModal', () => {
    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('renders nothing when closed', () => {
        const { container } = render(
            <TelegramLinkModal isOpen={false} linkRequestId="req-1" onClose={vi.fn()} />,
        );

        expect(container).toBeEmptyDOMElement();
    });

    it('renders the modal content when open', () => {
        render(<TelegramLinkModal isOpen linkRequestId="req-1" onClose={vi.fn()} />);

        expect(screen.getByText('telegramLink.title')).toBeInTheDocument();
    });

    it('closes when the close button is clicked', async () => {
        const onClose = vi.fn();
        render(<TelegramLinkModal isOpen linkRequestId="req-1" onClose={onClose} />);

        await userEvent.click(screen.getByRole('button', { name: 'actions.close' }));

        expect(onClose).toHaveBeenCalledOnce();
    });

    it('opens Telegram with the link request id and closes the modal', async () => {
        const openMock = vi.spyOn(globalThis, 'open').mockReturnValue(null);
        const onClose = vi.fn();
        render(<TelegramLinkModal isOpen linkRequestId="req-1" onClose={onClose} />);

        await userEvent.click(screen.getByRole('button', { name: 'telegramLink.openTelegram' }));

        expect(openMock).toHaveBeenCalledWith(
            'https://t.me/futures_and_other_arbitrage_bot?text=req-1',
            '_blank',
        );
        expect(onClose).toHaveBeenCalledOnce();
    });
});
