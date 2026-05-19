import React from 'react';
import TelegramIcon from '@mui/icons-material/Telegram';

interface TelegramLinkModalProps {
  isOpen: boolean;
  linkRequestId: string;
  onClose: () => void;
}

const TelegramLinkModal: React.FC<TelegramLinkModalProps> = ({ isOpen, linkRequestId, onClose }) => {
  if (!isOpen) return null;

  const handleLinkTelegram = () => {
    const botUsername = 'futures_and_other_arbitrage_bot';
    const telegramUrl = `https://t.me/${botUsername}?text=${linkRequestId}`;
    
    window.open(telegramUrl, '_blank');
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
      <div className="bg-white rounded-2xl shadow-xl max-w-lg w-full mx-4 overflow-hidden flex flex-col">
        <div className="p-6 border-b border-gray-200">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-12 h-12 bg-blue-100 rounded-full flex items-center justify-center">
                <TelegramIcon sx={{ fontSize: 28, color: '#0088cc' }} />
              </div>
              <h2 className="text-2xl font-bold text-gray-800">Link Telegram Account</h2>
            </div>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 transition-colors"
              aria-label="Close"
            >
              <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>
        
        <div className="p-6">
          <div className="space-y-4 text-gray-700">
            <p className="text-lg">
              Connect your Telegram account to receive instant notifications about profitable trading opportunities.
            </p>
            
            <div className="bg-blue-50 rounded-lg p-4 border border-blue-100">
              <h3 className="font-semibold text-gray-900 mb-2">What you'll get:</h3>
              <ul className="space-y-2 text-sm">
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 mt-0.5">✓</span>
                  <span>Real-time alerts for spread opportunities</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 mt-0.5">✓</span>
                  <span>Customized notifications based on your preferences</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 mt-0.5">✓</span>
                  <span>Direct access to trading insights</span>
                </li>
              </ul>
            </div>

            <p className="text-sm text-gray-600">
              Click the button below to open Telegram and complete the linking process with our bot.
            </p>
          </div>
        </div>
        
        <div className="p-6 border-t border-gray-200 flex justify-end gap-3">
          <button
            onClick={onClose}
            className="px-6 py-2.5 rounded-lg bg-gray-100 text-gray-700 font-medium hover:bg-gray-200 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleLinkTelegram}
            className="px-6 py-2.5 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 transition-colors shadow-sm flex items-center gap-2"
          >
            <TelegramIcon sx={{ fontSize: 20 }} />
            Open Telegram
          </button>
        </div>
      </div>
    </div>
  );
};

export default TelegramLinkModal;
